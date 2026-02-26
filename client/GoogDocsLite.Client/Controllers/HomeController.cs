using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using GoogDocsLite.Client.Models;
using GoogDocsLite.Client.Models.Home;
using GoogDocsLite.Client.Services;

namespace GoogDocsLite.Client.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly HealthApiClient _healthApiClient;

    // Constructorul primeste dependintele folosite de controller (logging + apel API health).
    public HomeController(ILogger<HomeController> logger, HealthApiClient healthApiClient)
    {
        _logger = logger;
        _healthApiClient = healthApiClient;
    }

    // Dashboard-ul principal: cere statusul serverului API si il afiseaza in view.
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var viewModel = new HomeIndexViewModel();

        try
        {
            var health = await _healthApiClient.GetStatusAsync(cancellationToken);
            viewModel.ServerStatus = health.Status;
            viewModel.ServerUtcTime = health.UtcTime;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch server health.");
            viewModel.ErrorMessage = ex.Message;
        }

        return View(viewModel);
    }

    // Pagina standard de eroare MVC, folosita pentru exceptii necontrolate.
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
