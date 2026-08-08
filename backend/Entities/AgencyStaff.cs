namespace FreightLink.Api.Entities;

public class AgencyStaff
{
    public Guid UserId { get; set; }
    public Guid AgencyId { get; set; }
    public string? JobTitle { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public User User { get; set; } = null!;
    public Agency Agency { get; set; } = null!;
}
