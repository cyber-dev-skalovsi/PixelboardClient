public interface IAuthService
{
    bool IsAuthenticated { get; }
    string Username { get; }
}

public class AuthService : IAuthService
{
    private readonly IHttpContextAccessor _contextAccessor;

    public AuthService(IHttpContextAccessor contextAccessor)
    {
        _contextAccessor = contextAccessor;
    }

    public bool IsAuthenticated => _contextAccessor.HttpContext.User.Identity.IsAuthenticated;
    public string Username => _contextAccessor.HttpContext.User.Identity.Name ?? "Gast";
}
