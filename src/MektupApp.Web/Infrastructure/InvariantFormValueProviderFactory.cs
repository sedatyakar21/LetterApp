using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace MektupApp.Web.Infrastructure;

/// <summary>
/// Form değerlerini kültürden bağımsız okur. Tarayıcılar &lt;input type="number"&gt; değerini her zaman
/// "290.5" biçiminde gönderir; tr-TR kültürüyle okununca ondalık nokta yanlış yorumlanırdı.
/// </summary>
public sealed class InvariantFormValueProviderFactory : IValueProviderFactory
{
    public async Task CreateValueProviderAsync(ValueProviderFactoryContext context)
    {
        var request = context.ActionContext.HttpContext.Request;
        if (!request.HasFormContentType) return;

        var form = await request.ReadFormAsync();
        context.ValueProviders.Add(new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture));
    }
}
