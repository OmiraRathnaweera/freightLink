using FreightLink.Api.Entities.Enums;

namespace FreightLink.Api.Entities;

public class Agency
{
    public Guid AgencyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string BusinessRegNo { get; set; } = string.Empty;
    public string YardAddress { get; set; } = string.Empty;
    public decimal YardLat { get; set; }
    public decimal YardLng { get; set; }
    public AgencyStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<AgencyStatusHistory> StatusHistory { get; set; } = new List<AgencyStatusHistory>();
    public ICollection<AgencyStaff> Staff { get; set; } = new List<AgencyStaff>();
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
    public ICollection<Driver> Drivers { get; set; } = new List<Driver>();
    public ICollection<ComplianceDoc> ComplianceDocs { get; set; } = new List<ComplianceDoc>();
    public ICollection<MatchCandidate> MatchCandidates { get; set; } = new List<MatchCandidate>();
    public ICollection<Assignment> Assignments { get; set; } = new List<Assignment>();
}
