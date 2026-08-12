using FreightLink.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace FreightLink.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ShipperProfile> ShipperProfiles => Set<ShipperProfile>();

    public DbSet<Agency> Agencies => Set<Agency>();
    public DbSet<AgencyStatusHistory> AgencyStatusHistories => Set<AgencyStatusHistory>();
    public DbSet<AgencyStaff> AgencyStaff => Set<AgencyStaff>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<ComplianceDoc> ComplianceDocs => Set<ComplianceDoc>();
    public DbSet<MatchCandidate> MatchCandidates => Set<MatchCandidate>();

    public DbSet<Load> Loads => Set<Load>();
    public DbSet<LoadStatusHistory> LoadStatusHistories => Set<LoadStatusHistory>();
    public DbSet<LoadFile> Files => Set<LoadFile>();
    public DbSet<UploadedFile> UploadedFiles => Set<UploadedFile>();

    public DbSet<Assignment> Assignments => Set<Assignment>();
    public DbSet<AssignmentResponse> AssignmentResponses => Set<AssignmentResponse>();
    public DbSet<Trip> Trips => Set<Trip>();
    public DbSet<TripEvent> TripEvents => Set<TripEvent>();
    public DbSet<TripEvidence> TripEvidences => Set<TripEvidence>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentWebhookEvent> PaymentWebhookEvents => Set<PaymentWebhookEvent>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<DisputeResolution> DisputeResolutions => Set<DisputeResolution>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public DbSet<AgentWorkflowRun> AgentWorkflowRuns => Set<AgentWorkflowRun>();
    public DbSet<AgentStep> AgentSteps => Set<AgentStep>();
    public DbSet<ToolCall> ToolCalls => Set<ToolCall>();
    public DbSet<ApprovalDecision> ApprovalDecisions => Set<ApprovalDecision>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // All enum-typed properties persist as their member name (readable in psql, safe to
        // reorder later) instead of EF's default int mapping. Applied once here instead of
        // repeating HasConversion<string>() in every IEntityTypeConfiguration<T>.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.ClrType.GetProperties())
            {
                var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (targetType.IsEnum)
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(property.Name)
                        .HasConversion<string>();
                }
            }
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
