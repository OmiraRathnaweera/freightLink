using System.Net;
using FreightLink.Api.Common.Errors;
using FreightLink.Api.Common.Exceptions;
using FreightLink.Api.Data;
using FreightLink.Api.DTOs.Agency;
using FreightLink.Api.Entities;
using FreightLink.Api.Entities.Enums;
using FreightLink.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FreightLink.Api.Services;

/// <inheritdoc cref="IAgencyService" />
public class AgencyService : IAgencyService
{
    private readonly AppDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;

    /// <summary>Creates the agency service with its DB context and password hasher.</summary>
    public AgencyService(AppDbContext dbContext, IPasswordHasher passwordHasher)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> CreateAsync(Guid currentUserId, UserRole currentUserRole, AgencyCreateDto request, CancellationToken cancellationToken = default)
    {
        var existingAgency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.BusinessRegNo == request.BusinessRegNo, cancellationToken);
            
        if (existingAgency != null)
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "An agency with this business registration number already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var agency = new Agency
        {
            AgencyId = Guid.NewGuid(),
            Name = request.Name,
            BusinessRegNo = request.BusinessRegNo,
            YardAddress = request.YardAddress,
            YardLat = request.YardLat,
            YardLng = request.YardLng,
            Status = AgencyStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now
        };

        var statusHistory = new AgencyStatusHistory
        {
            AgencyStatusHistoryId = Guid.NewGuid(),
            AgencyId = agency.AgencyId,
            ChangedByUserId = currentUserId,
            FromStatus = null,
            ToStatus = AgencyStatus.Pending,
            ChangedAt = now
        };

        _dbContext.Agencies.Add(agency);
        _dbContext.AgencyStatusHistories.Add(statusHistory);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505", ConstraintName: "uq_agency_regno" })
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.BUSINESS_REG_NO_ALREADY_REGISTERED, "An agency with this business registration number already exists.");
        }

        return MapToResponse(agency);
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> GetByIdAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies.AsNoTracking()
            .Include(a => a.Drivers)
            .Include(a => a.Vehicles)
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        return MapToResponse(agency);
    }

    /// <inheritdoc />
    public async Task<PagedAgencyResponseDto> GetListAsync(Guid currentUserId, UserRole currentUserRole, AgencyListQueryDto query, CancellationToken cancellationToken = default)
    {
        if (currentUserRole != UserRole.Admin)
        {
            throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Only administrators can list all agencies.");
        }

        IQueryable<Agency> dbQuery = _dbContext.Agencies.AsNoTracking()
            .Include(a => a.Drivers)
            .Include(a => a.Vehicles);

        if (query.Status.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Status == query.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.ToLower();
            dbQuery = dbQuery.Where(a => 
                a.Name.ToLower().Contains(search) || 
                a.BusinessRegNo.ToLower().Contains(search) ||
                a.YardAddress.ToLower().Contains(search));
        }

        // Default sort by CreatedAt desc
        dbQuery = query.SortBy?.ToLower() switch
        {
            "name" => query.SortDir?.ToLower() == "asc" ? dbQuery.OrderBy(a => a.Name) : dbQuery.OrderByDescending(a => a.Name),
            "status" => query.SortDir?.ToLower() == "asc" ? dbQuery.OrderBy(a => a.Status) : dbQuery.OrderByDescending(a => a.Status),
            _ => query.SortDir?.ToLower() == "asc" ? dbQuery.OrderBy(a => a.CreatedAt) : dbQuery.OrderByDescending(a => a.CreatedAt)
        };

        var totalItems = await dbQuery.CountAsync(cancellationToken);

        var agencies = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedAgencyResponseDto
        {
            Items = agencies.Select(MapToResponse).ToList(),
            Page = query.Page,
            PageSize = query.PageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)query.PageSize)
        };
    }

    /// <inheritdoc />
    public async Task<AgencyResponseDto> UpdateAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, AgencyUpdateDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies
            .FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);

        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (request.Name != null) agency.Name = request.Name;
        if (request.YardAddress != null) agency.YardAddress = request.YardAddress;
        if (request.YardLat.HasValue) agency.YardLat = request.YardLat.Value;
        if (request.YardLng.HasValue) agency.YardLng = request.YardLng.Value;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return MapToResponse(agency);
    }

    private async Task VerifyAgencyOwnershipAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken)
    {
        if (currentUserRole == UserRole.Admin)
        {
            return;
        }

        if (currentUserRole == UserRole.AgencyStaff)
        {
            var isOwned = await _dbContext.AgencyStaff
                .AnyAsync(s => s.UserId == currentUserId && s.AgencyId == agencyId, cancellationToken);

            if (!isOwned)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.AGENCY_NOT_OWNED, "You do not have permission to access this agency.");
            }
            
            return;
        }

        throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "Your role does not permit accessing agency data.");
    }

    /// <inheritdoc />
    public async Task<List<VehicleResponseDto>> GetVehiclesAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        var vehicles = await _dbContext.Vehicles.AsNoTracking()
            .Where(v => v.AgencyId == agencyId)
            .OrderBy(v => v.RegistrationNo)
            .ToListAsync(cancellationToken);

        return vehicles.Select(v => new VehicleResponseDto
        {
            VehicleId = v.VehicleId,
            AgencyId = v.AgencyId,
            RegistrationNo = v.RegistrationNo,
            VehicleType = v.VehicleType.ToString(),
            CapacityKg = v.CapacityKg,
            VolumeM3 = v.VolumeM3,
            Status = v.Status.ToString(),
            IsAvailable = v.Status == VehicleStatus.Available,
            CreatedAt = v.CreatedAt,
            UpdatedAt = v.UpdatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<List<DriverResponseDto>> GetDriversAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);
        var drivers = await _dbContext.Drivers.AsNoTracking()
            .Include(d => d.User)
            .Where(d => d.AgencyId == agencyId)
            .OrderBy(d => d.User.FullName)
            .ToListAsync(cancellationToken);

        return drivers.Select(d => new DriverResponseDto
        {
            DriverId = d.DriverId,
            UserId = d.UserId,
            AgencyId = d.AgencyId,
            FullName = d.User?.FullName ?? string.Empty,
            Email = d.User?.Email ?? string.Empty,
            LicenceNo = d.LicenceNo,
            LicenceExpiry = d.LicenceExpiry,
            Status = d.Status.ToString(),
            IsActive = d.Status == DriverStatus.Active,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        }).ToList();
    }

    /// <inheritdoc />
    public async Task<DriverResponseDto> AddDriverAsync(Guid agencyId, Guid currentUserId, UserRole currentUserRole, CreateDriverRequestDto request, CancellationToken cancellationToken = default)
    {
        await VerifyAgencyOwnershipAsync(agencyId, currentUserId, currentUserRole, cancellationToken);

        var agency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == agencyId, cancellationToken);
        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        if (request.LicenceExpiry <= DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Licence expiry date must be in the future.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(u => u.Email == normalizedEmail, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
        }

        if (await _dbContext.Drivers.AnyAsync(d => d.LicenceNo == request.LicenceNo, cancellationToken))
        {
            throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_LICENCE_ALREADY_REGISTERED, "A driver with this driving licence number already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Role = UserRole.Driver,
            Email = normalizedEmail,
            PasswordHash = _passwordHasher.Hash(request.Password),
            FullName = request.FullName,
            PhoneE164 = request.PhoneE164,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

        var driver = new Driver
        {
            DriverId = Guid.NewGuid(),
            UserId = user.UserId,
            AgencyId = agencyId,
            LicenceNo = request.LicenceNo,
            LicenceExpiry = request.LicenceExpiry,
            Status = DriverStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Users.Add(user);
        _dbContext.Drivers.Add(driver);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" } pg)
        {
            if (pg.ConstraintName == "uq_user_email")
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.EMAIL_ALREADY_REGISTERED, "An account with this email already exists.");
            }
            if (pg.ConstraintName == "IX_Drivers_LicenceNo")
            {
                throw new ApiException(HttpStatusCode.Conflict, ErrorCode.DRIVER_LICENCE_ALREADY_REGISTERED, "A driver with this driving licence number already exists.");
            }
            throw;
        }

        return new DriverResponseDto
        {
            DriverId = driver.DriverId,
            UserId = user.UserId,
            AgencyId = driver.AgencyId,
            FullName = user.FullName,
            Email = user.Email,
            LicenceNo = driver.LicenceNo,
            LicenceExpiry = driver.LicenceExpiry,
            Status = driver.Status.ToString(),
            IsActive = true,
            CreatedAt = driver.CreatedAt,
            UpdatedAt = driver.UpdatedAt
        };
    }

    /// <inheritdoc />
    public async Task<AgencyFleetResponseDto> GetFleetAsync(Guid? agencyId, Guid currentUserId, UserRole currentUserRole, CancellationToken cancellationToken = default)
    {
        Guid targetAgencyId;
        if (currentUserRole == UserRole.AgencyStaff)
        {
            var staff = await _dbContext.AgencyStaff.AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == currentUserId, cancellationToken);
            if (staff == null)
            {
                throw new ApiException(HttpStatusCode.Forbidden, ErrorCode.FORBIDDEN, "No agency staff profile found for caller.");
            }
            targetAgencyId = staff.AgencyId;
        }
        else if (agencyId.HasValue)
        {
            targetAgencyId = agencyId.Value;
        }
        else
        {
            throw new ApiException(HttpStatusCode.BadRequest, ErrorCode.VALIDATION_ERROR, "Agency ID must be specified.");
        }

        var agency = await _dbContext.Agencies.AsNoTracking()
            .FirstOrDefaultAsync(a => a.AgencyId == targetAgencyId, cancellationToken);
        if (agency == null)
        {
            throw new ApiException(HttpStatusCode.NotFound, ErrorCode.AGENCY_NOT_FOUND, "The requested agency could not be found.");
        }

        var vehicles = await GetVehiclesAsync(targetAgencyId, currentUserId, currentUserRole, cancellationToken);
        var drivers = await GetDriversAsync(targetAgencyId, currentUserId, currentUserRole, cancellationToken);

        return new AgencyFleetResponseDto
        {
            AgencyId = targetAgencyId,
            AgencyName = agency.Name,
            Vehicles = vehicles,
            Drivers = drivers
        };
    }

    /// <inheritdoc />
    public async Task SeedDefaultAgenciesIfNotExistsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var defaultPasswordHash = _passwordHasher.Hash("Password123!");

        var seedAgencies = new[]
        {
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224ba"),
                Name = "Samagi Express Logistics",
                RegNo = "PV-88991",
                Address = "45 Harbor Access Road, Peliyagoda",
                Lat = 6.9667m,
                Lng = 79.8917m,
                StaffEmail = "agency@freightlink.lk",
                StaffName = "Kasun Jayasuriya",
                Vehicles = new[]
                {
                    (RegNo: "WP-CAB-4521", Type: VehicleType.Lorry, CapKg: 6500m, VolM3: 30m),
                    (RegNo: "WP-DA-8920", Type: VehicleType.Container, CapKg: 18000m, VolM3: 65m),
                    (RegNo: "WP-LB-6712", Type: VehicleType.FlatBed, CapKg: 12000m, VolM3: 45m),
                    (RegNo: "WP-ND-1102", Type: VehicleType.Lorry, CapKg: 1400m, VolM3: 5.5m)
                },
                Drivers = new[]
                {
                    (Email: "suneth.driver@samagi.lk", Name: "Suneth Alwis", Phone: "+94771234501", LicenceNo: "B-8839210", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "kamal.driver@samagi.lk", Name: "Kamal Weerasinghe", Phone: "+94771234502", LicenceNo: "B-8839211", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bb"),
                Name = "Lanka Freight & Cargo Hub",
                RegNo = "PV-77102",
                Address = "120 Baseline Road, Dematagoda, Colombo 09",
                Lat = 6.9380m,
                Lng = 79.8780m,
                StaffEmail = "lanka.staff@freightlink.lk",
                StaffName = "Dinesh Perera",
                Vehicles = new[]
                {
                    (RegNo: "WP-LC-1029", Type: VehicleType.Lorry, CapKg: 3500m, VolM3: 14m),
                    (RegNo: "WP-LH-5519", Type: VehicleType.Lorry, CapKg: 8500m, VolM3: 36m),
                    (RegNo: "WP-CX-9011", Type: VehicleType.Container, CapKg: 24000m, VolM3: 70m),
                    (RegNo: "WP-ND-2045", Type: VehicleType.Lorry, CapKg: 1200m, VolM3: 5.0m)
                },
                Drivers = new[]
                {
                    (Email: "priyantha.driver@lankafreight.lk", Name: "Priyantha Kumara", Phone: "+94771234503", LicenceNo: "B-7740192", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "ruwan.driver@lankafreight.lk", Name: "Ruwan Gamage", Phone: "+94771234504", LicenceNo: "B-7740193", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bc"),
                Name = "Southern Coastal Haulage",
                RegNo = "PV-65401",
                Address = "78 Matara Road, Galle",
                Lat = 6.0354m,
                Lng = 80.2170m,
                StaffEmail = "southern.staff@freightlink.lk",
                StaffName = "Rohan Mendis",
                Vehicles = new[]
                {
                    (RegNo: "SP-SC-4401", Type: VehicleType.Lorry, CapKg: 5000m, VolM3: 22m),
                    (RegNo: "SP-SC-8812", Type: VehicleType.Container, CapKg: 19000m, VolM3: 55m),
                    (RegNo: "SP-SC-1092", Type: VehicleType.Lorry, CapKg: 1500m, VolM3: 6.0m)
                },
                Drivers = new[]
                {
                    (Email: "anura.driver@southernhaul.lk", Name: "Anura Jayatissa", Phone: "+94771234506", LicenceNo: "B-6591023", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "lasantha.driver@southernhaul.lk", Name: "Lasantha Fernando", Phone: "+94771234507", LicenceNo: "B-6591024", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bd"),
                Name = "Central Highlands Express",
                RegNo = "PV-91044",
                Address = "15 Kandy Industrial Estate, Peradeniya",
                Lat = 7.2600m,
                Lng = 80.5980m,
                StaffEmail = "kandy.staff@freightlink.lk",
                StaffName = "Nuwan Bandara",
                Vehicles = new[]
                {
                    (RegNo: "CP-CH-1205", Type: VehicleType.Lorry, CapKg: 4000m, VolM3: 18m),
                    (RegNo: "CP-CH-6520", Type: VehicleType.Lorry, CapKg: 9000m, VolM3: 38m),
                    (RegNo: "CP-CH-8901", Type: VehicleType.Container, CapKg: 20000m, VolM3: 60m),
                    (RegNo: "CP-CH-3310", Type: VehicleType.Lorry, CapKg: 1300m, VolM3: 5.2m)
                },
                Drivers = new[]
                {
                    (Email: "nihal.driver@centralhigh.lk", Name: "Nihal Bandara", Phone: "+94771234508", LicenceNo: "B-9140221", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "janaka.driver@centralhigh.lk", Name: "Janaka Senanayake", Phone: "+94771234509", LicenceNo: "B-9140222", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224be"),
                Name = "Wayamba Regional Transporters",
                RegNo = "PV-44320",
                Address = "34 Dambulla Road, Kurunegala",
                Lat = 7.4863m,
                Lng = 80.3623m,
                StaffEmail = "wayamba.staff@freightlink.lk",
                StaffName = "Charith Wickramasinghe",
                Vehicles = new[]
                {
                    (RegNo: "NW-WR-3310", Type: VehicleType.Lorry, CapKg: 7500m, VolM3: 32m),
                    (RegNo: "NW-WR-7799", Type: VehicleType.Container, CapKg: 22000m, VolM3: 62m),
                    (RegNo: "NW-WR-1188", Type: VehicleType.Lorry, CapKg: 1450m, VolM3: 5.8m)
                },
                Drivers = new[]
                {
                    (Email: "sisira.driver@wayambafreight.lk", Name: "Sisira Dissanayake", Phone: "+94771234510", LicenceNo: "B-4410982", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4))),
                    (Email: "asanka.driver@wayambafreight.lk", Name: "Asanka Ranatunga", Phone: "+94771234511", LicenceNo: "B-4410983", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3)))
                }
            },
            new
            {
                Id = Guid.Parse("258466d2-3d76-46c5-9eb4-ede1f49224bf"),
                Name = "Kelani Valley Logistics Hub",
                RegNo = "PV-55109",
                Address = "88 Biyagama Free Trade Zone Road, Kelaniya",
                Lat = 6.9535m,
                Lng = 79.9240m,
                StaffEmail = "kelani.staff@freightlink.lk",
                StaffName = "Harsha Gunawardena",
                Vehicles = new[]
                {
                    (RegNo: "WP-KV-1011", Type: VehicleType.Lorry, CapKg: 1500m, VolM3: 6.0m),
                    (RegNo: "WP-KV-4420", Type: VehicleType.Lorry, CapKg: 7000m, VolM3: 28m),
                    (RegNo: "WP-KV-9901", Type: VehicleType.Container, CapKg: 25000m, VolM3: 68m)
                },
                Drivers = new[]
                {
                    (Email: "mahesh.driver@kelanifreight.lk", Name: "Mahesh Senarath", Phone: "+94771234512", LicenceNo: "B-5519821", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(3))),
                    (Email: "tharindu.driver@kelanifreight.lk", Name: "Tharindu Perera", Phone: "+94771234513", LicenceNo: "B-5519822", Expiry: DateOnly.FromDateTime(DateTime.UtcNow.AddYears(4)))
                }
            }
        };

        foreach (var item in seedAgencies)
        {
            var agency = await _dbContext.Agencies.FirstOrDefaultAsync(a => a.AgencyId == item.Id || a.BusinessRegNo == item.RegNo, cancellationToken);
            if (agency == null)
            {
                agency = new Agency
                {
                    AgencyId = item.Id,
                    Name = item.Name,
                    BusinessRegNo = item.RegNo,
                    YardAddress = item.Address,
                    YardLat = item.Lat,
                    YardLng = item.Lng,
                    Status = AgencyStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Agencies.Add(agency);
            }
            else
            {
                agency.Status = AgencyStatus.Active;
                agency.YardAddress = item.Address;
                agency.YardLat = item.Lat;
                agency.YardLng = item.Lng;
                agency.UpdatedAt = now;
            }

            // Ensure Staff user exists
            var staffUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == item.StaffEmail, cancellationToken);
            if (staffUser == null)
            {
                staffUser = new User
                {
                    UserId = Guid.NewGuid(),
                    Role = UserRole.AgencyStaff,
                    Email = item.StaffEmail,
                    FullName = item.StaffName,
                    PasswordHash = defaultPasswordHash,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                };
                _dbContext.Users.Add(staffUser);
            }

            var hasStaffLink = await _dbContext.AgencyStaff.AnyAsync(s => s.UserId == staffUser.UserId && s.AgencyId == agency.AgencyId, cancellationToken);
            if (!hasStaffLink)
            {
                _dbContext.AgencyStaff.Add(new AgencyStaff
                {
                    UserId = staffUser.UserId,
                    AgencyId = agency.AgencyId,
                    JobTitle = "Operations Dispatcher",
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }

            // Ensure Fleet Vehicles exist
            foreach (var v in item.Vehicles)
            {
                var vehicleExists = await _dbContext.Vehicles.AnyAsync(veh => veh.RegistrationNo == v.RegNo, cancellationToken);
                if (!vehicleExists)
                {
                    _dbContext.Vehicles.Add(new Vehicle
                    {
                        VehicleId = Guid.NewGuid(),
                        AgencyId = agency.AgencyId,
                        RegistrationNo = v.RegNo,
                        VehicleType = v.Type,
                        CapacityKg = v.CapKg,
                        VolumeM3 = v.VolM3,
                        Status = VehicleStatus.Available,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }

            // Ensure Real Drivers exist
            foreach (var d in item.Drivers)
            {
                var driverUser = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == d.Email, cancellationToken);
                if (driverUser == null)
                {
                    driverUser = new User
                    {
                        UserId = Guid.NewGuid(),
                        Role = UserRole.Driver,
                        Email = d.Email,
                        FullName = d.Name,
                        PhoneE164 = d.Phone,
                        PasswordHash = defaultPasswordHash,
                        IsActive = true,
                        CreatedAt = now,
                        UpdatedAt = now
                    };
                    _dbContext.Users.Add(driverUser);
                }

                var driverExists = await _dbContext.Drivers.AnyAsync(dr => dr.LicenceNo == d.LicenceNo || dr.UserId == driverUser.UserId, cancellationToken);
                if (!driverExists)
                {
                    _dbContext.Drivers.Add(new Driver
                    {
                        DriverId = Guid.NewGuid(),
                        UserId = driverUser.UserId,
                        AgencyId = agency.AgencyId,
                        LicenceNo = d.LicenceNo,
                        LicenceExpiry = d.Expiry,
                        Status = DriverStatus.Active,
                        CreatedAt = now,
                        UpdatedAt = now
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static AgencyResponseDto MapToResponse(Agency agency)
    {
        return new AgencyResponseDto
        {
            AgencyId = agency.AgencyId,
            Name = agency.Name,
            BusinessRegNo = agency.BusinessRegNo,
            YardAddress = agency.YardAddress,
            YardLat = agency.YardLat,
            YardLng = agency.YardLng,
            Status = agency.Status,
            DriverCount = agency.Drivers?.Count ?? 0,
            ActiveDriverCount = agency.Drivers?.Count(d => d.Status == DriverStatus.Active) ?? 0,
            VehicleCount = agency.Vehicles?.Count ?? 0,
            CreatedAt = agency.CreatedAt,
            UpdatedAt = agency.UpdatedAt
        };
    }
}
