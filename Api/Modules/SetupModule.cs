using FluentValidation;
using FluentValidation.AspNetCore;

namespace Api.Modules;

public static class SetupModule
{
    public static void SetupServices(this IServiceCollection services)
    {
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<Program>();
    }
}
