using System;
using System.Text;
using System.Threading.Tasks;
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

       public ProcessService(string clientId, string clientSecret, string clusterUrl)
        {
            _zeebeClient = CamundaCloudClientBuilder
                .Builder()
                .UseClientId(clientId)
                .UseClientSecret(clientSecret)
              .UseContactPoint(clusterUrl)
                .Build();
        }

        public async Task<string> DeployWorkflow(string bpmnFile)
        {
            var encoding = Encoding.UTF8; // or any other encoding you prefer
            var bytes = encoding.GetBytes(bpmnFile);
            var deployResponse = await _zeebeClient.NewDeployCommand()
                .AddResourceString(bpmnFile,encoding,"process.bpmn")
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
        }

        public void CreateHelloWorker(){
            CreateWorker("print-hello", async (client, job) =>
            {
                Console.WriteLine("Working on Task");
                Console.WriteLine("HELLOOOOOOOOOOOOO!!!");
                Console.WriteLine("Completed the fetched Task");
                await client.NewCompleteJobCommand(job.Key).Send();
            });
        }

        public void CreateWorker(string jobType, JobHandler handleJob){
             _zeebeClient.NewWorker()
                       .JobType(jobType)
                       .Handler(handleJob)
                       .MaxJobsActive(5)
                       .Name(Environment.MachineName)
                       .AutoCompletion()
                       .PollInterval(TimeSpan.FromSeconds(1))
                       .Timeout(TimeSpan.FromSeconds(10))
                       .Open();
        }
    }
}