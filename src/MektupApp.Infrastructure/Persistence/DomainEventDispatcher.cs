using MektupApp.Application.Abstractions;
using MektupApp.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MektupApp.Infrastructure.Persistence;

/// <summary>Domain event'ini DI'a kayıtlı tüm IDomainEventHandler&lt;T&gt; uygulamalarına iletir.</summary>
public sealed class DomainEventDispatcher(IServiceProvider services, ILogger<DomainEventDispatcher> logger)
{
    public async Task DispatchAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        foreach (var handler in services.GetServices(handlerType))
        {
            try
            {
                await (Task)handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.HandleAsync))!
                    .Invoke(handler, [domainEvent, ct])!;
            }
            catch (Exception ex)
            {
                // Bildirim hatası ana işlemi (ödeme, kargo) geri almamalı.
                logger.LogError(ex, "Domain event işlenemedi: {Event}", domainEvent.GetType().Name);
            }
        }
    }
}
