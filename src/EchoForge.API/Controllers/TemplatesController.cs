using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplatesController : ControllerBase
{
    private readonly ITemplateService _templateService;

    public TemplatesController(ITemplateService templateService)
    {
        _templateService = templateService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Template>>> GetAll()
    {
        var templates = await _templateService.GetAllAsync();
        return Ok(templates);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Template>> GetById(int id)
    {
        var template = await _templateService.GetByIdAsync(id);
        if (template == null) return NotFound();
        return Ok(template);
    }

    [HttpPost]
    public async Task<ActionResult<Template>> Create([FromBody] Template template)
    {
        var created = await _templateService.CreateAsync(template);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Template>> Update(int id, [FromBody] Template template)
    {
        template.Id = id;
        var updated = await _templateService.UpdateAsync(template);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        await _templateService.DeleteAsync(id);
        return NoContent();
    }
}
