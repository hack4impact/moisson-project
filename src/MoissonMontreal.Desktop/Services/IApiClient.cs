using System.Threading.Tasks;

namespace MoissonMontreal.Desktop.Services;

public interface IApiClient
{
    Task<bool> IsApiReachableAsync();
}
