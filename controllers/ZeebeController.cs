using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Services;

namespace Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ZeebeController : ControllerBase
    {
        private readonly IZeebeService _zeebeService;

        public ZeebeController(IZeebeService zeebeService)
        {
            _zeebeService = zeebeService;
        }

        [HttpPost("run")]
        public async Task<ActionResult> RunBpmn([FromBody] BpmnXmlRequest bpmnXmlRequest)
        {
            try
            {
                var deploymentKey = await _zeebeService.DeployWorkflow(bpmnXmlRequest.BpmnXml);
                var processInstanceKey = await _zeebeService.StartWorkflowInstance(deploymentKey);

                return Ok(new
                {
                    Message = "BPMN deployed and process instance started successfully.",
                    DeploymentKey = deploymentKey,
                    ProcessInstanceKey = processInstanceKey
                });
            }

            catch (Exception ex)
            {
                return StatusCode(500, new { ErrorMessage = $"Internal Server Error: {ex.Message}" });
            }
        }

    }
}

public class BpmnXmlRequest
{
    [Required]
    [JsonPropertyName("bpmnXml")]
    public string BpmnXml { get; set; }
}