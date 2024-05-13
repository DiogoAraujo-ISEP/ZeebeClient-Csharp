using System;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Zeebe.Client.Impl.Builder;

namespace Services
{
    public interface IZeebeService
    {
        public Task<string> DeployWorkflow(string bpmnDefinition);
        public Task<long> StartWorkflowInstance(string bpmProcessId);
        public void StartWorkers();

    }
    public class ProcessService : IZeebeService
    {
        private readonly IZeebeClient _zeebeClient;
        private readonly IDatabaseInfrastructure _dbInfrastructure;

        public ProcessService(string clientId, string clientSecret, string clusterUrl, IDatabaseInfrastructure dbInfrastructure)
        {
            _zeebeClient = CamundaCloudClientBuilder
                .Builder()
                .UseClientId(clientId)
                .UseClientSecret(clientSecret)
              .UseContactPoint(clusterUrl)
                .Build();

            _dbInfrastructure = dbInfrastructure;




        }

        public async Task<string> DeployWorkflow(string bpmnFile)
        {
            var encoding = Encoding.UTF8;
            var bytes = encoding.GetBytes(bpmnFile);
            var deployResponse = await _zeebeClient.NewDeployCommand()
                .AddResourceString(bpmnFile, encoding, "process.bpmn")
                .Send();
            Console.WriteLine("Process Definition has been deployed!");

            var bpmnProcessId = deployResponse.Processes[0].BpmnProcessId;
            return bpmnProcessId;
        }

        public async Task<long> StartWorkflowInstance(string bpmnProcessId)
        {
            var processInstanceResponse = await _zeebeClient
                .NewCreateProcessInstanceCommand()
                .BpmnProcessId(bpmnProcessId)
                .LatestVersion()
                .Send();

            Console.WriteLine("Process Instance has been started!");
            var processInstanceKey = processInstanceResponse.ProcessInstanceKey;
            return processInstanceKey;
        }

        public async void StartWorkers()
        {
            await Task.Run(CreateHelloWorker);
            await Task.Run(CreateApprovalEmailWorker);
            await Task.Run(CreateRejectionEmailWorker);
            await Task.Run(CreateGetInterviewerWorker);
        }

        public void CreateHelloWorker()
        {
            Console.WriteLine("CREATED HELLO");
            CreateWorker("print-hello", async (client, job) =>
            {
                Console.WriteLine("Working on Task");
                Console.WriteLine("HELLOOOOOOOOOOOOO!!!");
                Console.WriteLine("Completed the fetched Task");
                await client.NewCompleteJobCommand(job.Key).Send();
            });
        }

        public void CreateApprovalEmailWorker()
        {
            Console.WriteLine("CREATED APPROVAL EMAIL WORKER");

            CreateWorker("approval-email", async (client, job) =>
            {
                try
                {
                    Console.WriteLine("APPROVAL EMAIL WORKER - STARTING JOB");
                    JObject jsonObject = JObject.Parse(job.Variables);

                    string email = (string)jsonObject["email"];
                    string name = (string)jsonObject["name"];
                    string interviewer = (string)jsonObject["interviewer"];

                    var subject = "Resposta entrevista emprego";
                    var body = $"O/A senhor(a) {name} foi aceite!\nO/A entrevistador(a) responsável foi {interviewer}.";

                    // Send email using the extracted variables
                    await SendEmail(email, subject, interviewer, body);

                    // Complete the job
                    await client.NewCompleteJobCommand(job.Key)
                        .Send();
                    Console.WriteLine("APPROVAL EMAIL WORKER - JOB DONE");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to complete job with key {job.Key}: {ex.Message}");
                }

            });
        }

        public void CreateRejectionEmailWorker()
        {
            Console.WriteLine("CREATED REJECTION EMAIL WORKER");

            CreateWorker("rejection-email", async (client, job) =>
            {
                try
                {

                    Console.WriteLine($"Job with key {job.Key}");

                    JObject jsonObject = JObject.Parse(job.Variables);

                    string email = (string)jsonObject["email"];
                    string name = (string)jsonObject["name"];
                    string interviewer = (string)jsonObject["interviewer"];

                    Console.WriteLine("REJECTION EMAIL WORKER - STARTING JOB");
                  
                    var subject = "Resposta entrevista emprego";
                    var body = $"O/A senhor(a) {name} foi rejeitado!\nO/A entrevistador(a) responsável foi {interviewer}.";

                    // Send email using the extracted variables
                    await SendEmail(email, subject, interviewer, body);

                    // Complete the job
                    await client.NewCompleteJobCommand(job.Key)
                        .Send();
                    Console.WriteLine("REJECTION EMAIL WORKER - JOB DONE");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to complete job with key {job.Key}: {ex.Message}");
                }

            });
        }

        private async Task SendEmail(string email, string subject, string interviewer, string body)
        {
            try
            {

                MailMessage message = new MailMessage();
                message.From = new MailAddress("template41425352425@hotmail.com");
                message.To.Add(email);
                message.Subject = subject;
                message.Body = body;

                SmtpClient smtpClient = new SmtpClient("smtp-mail.outlook.com");
                smtpClient.Port = 587; // Port number may vary
                smtpClient.Credentials = new System.Net.NetworkCredential("template41425352425@hotmail.com", "test1423!");
                smtpClient.EnableSsl = true; // Enable SSL if required by the SMTP server

                await smtpClient.SendMailAsync(message);
                Console.WriteLine("Email sent successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to send email: " + ex.Message);
                // Handle exceptions
            }
        }

        public void CreateGetInterviewerWorker()
        {
            Console.WriteLine("CREATED GET INTERVIEWER");
            CreateWorker("get-interviewer", async (client, job) =>
            {
                // Retrieve email parameter from the job payload
                JObject jsonObject = JObject.Parse(job.Variables);

                // Check if the email property exists in the jsonObject
                if (jsonObject["email"] != null)
                {
                    string email = (string)jsonObject["email"];

                    // Query the database infrastructure to get the interviewer by email
                    string interviewer = await _dbInfrastructure.GetInterviewerByEmailAsync(email);

                    if (!string.IsNullOrEmpty(interviewer))
                    {
                        Console.WriteLine($"Interviewer found: {interviewer}");

                        // Create a dictionary with the interviewer variable
                        Dictionary<string, string> interviewr = new Dictionary<string, string>
                        {
                    { "interviewer", interviewer }
                        };

                        // Complete the job with the interviewer variable
                        await client.NewCompleteJobCommand(job.Key)
                                    .Variables(JsonConvert.SerializeObject(interviewr))
                                    .Send();
                    }
                    else
                    {
                        Console.WriteLine("Interviewer not found.");

                        // Create a dictionary with an empty interviewer variable
                        Dictionary<string, string> interviewr = new Dictionary<string, string>
                        {
                    { "interviewer", string.Empty }
                        };

                        // Complete the job with the empty interviewer variable
                        await client.NewCompleteJobCommand(job.Key)
                                    .Variables(JsonConvert.SerializeObject(interviewr))
                                    .Send();
                    }
                }
                else
                {
                    Console.WriteLine("Email property not found in the payload.");
                    // Handle the case where the email property is missing in the payload
                }
            });
        }




        public void CreateWorker(string jobType, JobHandler handleJob)
        {
            _zeebeClient.NewWorker()
                      .JobType(jobType)
                      .Handler(handleJob)
                      .MaxJobsActive(1)
                      .Name(Environment.MachineName)
                      .AutoCompletion()
                      .PollInterval(TimeSpan.FromSeconds(1))
                      .PollingTimeout(TimeSpan.FromSeconds(1))
                      .Timeout(TimeSpan.FromSeconds(1))
                      .Open();
        }
    }
}

// Interface for the database infrastructure
public interface IDatabaseInfrastructure
{
    Task<string> GetInterviewerByEmailAsync(string email);
}

// Mock implementation of the database infrastructure class
public class MockDatabaseInfrastructure : IDatabaseInfrastructure
{
    public async Task<string> GetInterviewerByEmailAsync(string email)
    {
        // For demonstration purposes, let's return a mock interviewer name
        return await Task.FromResult("John Doe");
    }
}