using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PathBinder.Controllers;

[ApiController]
[Authorize]                     
[Route("api/files")]
public class FilesApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userMgr;
    private readonly IConfiguration _configuration;

    public FilesApiController(ApplicationDbContext db, UserManager<ApplicationUser> userMgr, IConfiguration configuration)
    {
        _db = db;
        _userMgr = userMgr;
        _configuration = configuration;
    }

    public record FileLibraryDto(string Id, string Name, string Cover);
    public record FileNameDto(string? Name);
    public record FileWriteDto
    {
        [JsonConstructor]
        public FileWriteDto(string? name = null, string? content = null, string? cover = null, string? styles = null)
        {
            Name = name;
            Content = content;
            Cover = cover;
            Styles = styles;
        }

        public string? Name { get; }
        public string? Content { get; }
        public string? Cover { get; }
        public string? Styles { get; }
    }
    public record UrlRequest(string Url);

    [HttpGet]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FileLibraryDto>>> GetAll(string sortKey = "lastModified", string sortOrder = "desc")
    {
        var userId = _userMgr.GetUserId(User)!;

        var query = _db.Files.Where(file => file.UserId == userId);

        query = (sortKey.ToLower(), sortOrder.ToLower()) switch
        {
            ("name", "asc") => query.OrderBy(f => f.Name.ToLower()),
            ("name", "desc") => query.OrderByDescending(f => f.Name.ToLower()),
            ("lastmodified", "asc") => query.OrderBy(f => f.LastModified),
            _ => query.OrderByDescending(f => f.LastModified)
        };

        var fileEntities = await query.ToListAsync();

        var fileListDtos = fileEntities
            .Select(f => new FileLibraryDto(f.Id, f.Name, f.Cover))
            .ToList();

        return Ok(fileListDtos);
    }



    [HttpGet("{id}")]
    public async Task<ActionResult<FileWriteDto>> GetOne(string id)
    {
        var uid = _userMgr.GetUserId(User)!;
        var row = await _db.Files.FirstOrDefaultAsync(f => f.Id == id && f.UserId == uid);
        if (row is null) return NotFound();
        return Ok(new FileWriteDto(row.Name, row.Content, row.Cover, row.Styles));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FileWriteDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            return BadRequest("Name is required.");

        var uid = _userMgr.GetUserId(User)!;
        var templatePath = _configuration["FileTemplates:DefaultTemplatePath"];
        if (string.IsNullOrWhiteSpace(templatePath) || !System.IO.File.Exists(templatePath))
            return StatusCode(500, "Template file missing or misconfigured.");

        var templateContent = await System.IO.File.ReadAllTextAsync(templatePath);
        templateContent = templateContent.Replace("\\n", Environment.NewLine).Replace("\\\"", "\"");

        var regex = new Regex(@"{{image\s+(https?:\/\/[^\s\[]+)", RegexOptions.IgnoreCase);

        string extractedImageUrl = null;
        if (!string.IsNullOrEmpty(dto.Content))
        {
            var match = regex.Match(dto.Content);
            if (match.Success && match.Groups.Count > 1)
            {
                extractedImageUrl = match.Groups[1].Value;
            }
        }

        var file = new FileItem
        {
            UserId = uid,
            Name = string.IsNullOrEmpty(dto.Name) ? "Untitled" : dto.Name,
            Content = string.IsNullOrEmpty(dto.Content) ? templateContent : dto.Content,
            Cover = extractedImageUrl ?? "https://i.imgur.com/9TU96xY.jpg",
            Styles = dto.Styles ?? "{}",
            LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
        _db.Files.Add(file);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] FileWriteDto dto)
    {
        var uid = _userMgr.GetUserId(User)!;
        var row = await _db.Files.FirstOrDefaultAsync(f => f.Id == id && f.UserId == uid);
        if (row is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(dto.Name))
            row.Name = dto.Name!;
        if (dto.Content is not null)
        {
            row.Content = dto.Content;
            if (string.IsNullOrWhiteSpace(dto.Cover))
            {
                var img = Regex.Match(dto.Content,
                    @"{{image\s+(https?:\/\/[^\s\[]+)",
                    RegexOptions.IgnoreCase);

                if (img.Success && img.Groups.Count > 1)
                    row.Cover = img.Groups[1].Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(dto.Cover))
            row.Cover = dto.Cover!;

        if (dto.Styles is not null)
            row.Styles = dto.Styles;

        row.LastModified = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        await _db.SaveChangesAsync();

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id)
    {
        var uid = _userMgr.GetUserId(User)!;
        var row = await _db.Files.FirstOrDefaultAsync(f => f.Id == id && f.UserId == uid);
        if (row is null) return NotFound();

        _db.Files.Remove(row);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("import")]
    public async Task<IActionResult> ImportHtml([FromBody] UrlRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Url))
            return BadRequest("URL is required.");

        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (compatible; PathBinder/1.0)");
            var html = await http.GetStringAsync(req.Url);
            return Content(html, "text/html");
        }
        catch (Exception ex)
        {
            return BadRequest("Fetch failed: " + ex.Message);
        }
    }
}
