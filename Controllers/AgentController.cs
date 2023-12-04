using api.Models;
using Microsoft.AspNetCore.Mvc;
using tidybee_hub.Repository;


namespace tidybee_hub.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly AgentRepository _agentRepository;

    public AgentController(AgentRepository agentRepository)
    {
        _agentRepository = agentRepository;
    }

    [HttpGet]
    public IActionResult GetAllAgents(bool includeMetadata = false, bool includeConnectionInformation = false)
    {
        var agents = _agentRepository.GetAllAgents(includeMetadata, includeConnectionInformation);
        return Ok(agents);
    }

    [HttpGet("deleted")]
    public IActionResult GetAllDeletedAgents(bool includeMetadata = false, bool includeConnectionInformation = false)
    {
        var agents = _agentRepository.GetAllAgentsByStatus(AgentStatusModel.Deleted, includeMetadata, includeConnectionInformation);
        return Ok(agents);
    }

    [HttpGet("{id}")]
    public IActionResult GetAgentById(Guid id, bool includeMetadata = false, bool includeConnectionInformation = false)
    {
        var agent = _agentRepository.GetAgentById(id, includeMetadata, includeConnectionInformation);
        if (agent == null)
            return NotFound();

        return Ok(agent);
    }

    [HttpPost]
    public IActionResult AddAgent([FromBody] AgentModel agent)
    {
        _agentRepository.AddAgent(agent);
        return CreatedAtAction(nameof(GetAgentById), new { id = agent.Uuid }, agent);
    }
}
