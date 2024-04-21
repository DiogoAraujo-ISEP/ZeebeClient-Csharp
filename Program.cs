using Newtonsoft.Json.Linq;
using Zeebe.Client;
using Zeebe.Client.Api.Responses;
using Zeebe.Client.Api.Worker;
using Zeebe.Client.Impl.Builder;

namespace Client
{
    internal class Program
    {
        
        private static readonly string _ClientID = "977PhrpiEB_ZcHBcw4zfFUX.qLNjZsXO";
        private static readonly string _ClientSecret = ".FvOYAbfu9rQrjOJ7hBQE41GNLHPgBt4O9dl9LjlHwfvi~21lspQnbG0uwqWdmS~";
        private static readonly string _ClusterURL = "aa103162-466b-4384-b687-78100c16286f.bru-2.zeebe.camunda.io";
        private static readonly String _BpmnFile = "D:/Documents/Mycloudstarter/resources/hello-process.bpmn";
        private static readonly String _JobType = "print-hello";

        public static IZeebeClient zeebeClient;

        static async Task Main(string[] args)
        {
            zeebeClient = CamundaCloudClientBuilder
                .Builder()
                .UseClientId(_ClientID)
                .UseClientSecret(_ClientSecret)
                .UseContactPoint(_ClusterURL)
                .Build();

            string bpmnProcessId = await DeployProcess(_BpmnFile);
            long processInstanceKey = await StartProcessInstance(bpmnProcessId);

            // Starting the Job Worker
            using (var signal = new EventWaitHandle(false,
            EventResetMode.AutoReset))
            {
                zeebeClient.NewWorker()
                           .JobType(_JobType)
                           .Handler(TriggerApproximation)
                           .MaxJobsActive(5)
                           .Name(Environment.MachineName)
                           .AutoCompletion()
                           .PollInterval(TimeSpan.FromSeconds(1))
                           .Timeout(TimeSpan.FromSeconds(10))
                           .Open();

                signal.WaitOne();
            }

        }

        private async static Task<string> DeployProcess(String bpmnFile)
        {
            var deployRespone = await zeebeClient.NewDeployCommand()
                .AddResourceFile(bpmnFile)
                .Send();
            Console.WriteLine("Process Definition has been deployed!");

            var bpmnProcessId = deployRespone.Processes[0].BpmnProcessId;
            return bpmnProcessId;
        }

        private async static Task<long> StartProcessInstance(string bpmnProcessId)
        {
            var processInstanceResponse = await zeebeClient
                            .NewCreateProcessInstanceCommand()
                            .BpmnProcessId(bpmnProcessId)
                            .LatestVersion()
                            .Send();

            Console.WriteLine("Process Instance has been started!");
            var processInstanceKey = processInstanceResponse.ProcessInstanceKey;
            return processInstanceKey;
        }

        private static void TriggerApproximation(IJobClient jobClient, IJob job)
        {

            Console.WriteLine("Working on Task");
            Console.WriteLine("HELLOOOOOOOOOOOOO!!!");
            Console.WriteLine("Completed the fetched Task");
        }


    }
}
