using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

public class FileItem
{
    [Key]
    [MaxLength(26)]
    public string Id { get; set; } = Ulid.NewUlid().ToString().ToLower();

    [MaxLength(128)]
    public string Name { get; set; } = default!;  

    [MaxLength(1024)]
    public string Cover { get; set; } = default!; 
    public string UserId { get; set; } = default!;
    public ApplicationUser? User { get; set; }

    [Column(TypeName = "TEXT")]
    public string Content { get; set; } = default!;

    [Column(TypeName = "TEXT")]
    public string Styles { get; set; } = "{}";

    public long LastModified { get; set; }
}

