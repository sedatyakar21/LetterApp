using MektupApp.Application.Admin;
using Microsoft.AspNetCore.Mvc;

namespace MektupApp.Web.Areas.Admin.Controllers;

public class DashboardController(IAdminQueries queries) : AdminControllerBase
{
    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await queries.GetDashboardAsync(ct));
}
