namespace Travel.Api.Dtos;

/// <summary>Team member in API response/request. Id is optional on PUT (backend-generated for new cards).</summary>
public record AgencyTeamMemberDto(string? Id, string Name, string Title, string ImageUrl);

/// <summary>Agency section content. Same shape for GET response and PUT body.</summary>
public record AgencySectionDto(
    string? Heading,
    string? Subtitle,
    string? Description,
    string? LogoUrl,
    string? BrandName,
    List<AgencyTeamMemberDto>? Team
);
