using Microsoft.AspNetCore.Identity;

namespace Plenaro.Api.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
}
