using MektupApp.Domain.Common;

namespace MektupApp.Domain.Letters;

public sealed record LetterPaid(Guid LetterId, Guid CustomerId, decimal Amount) : DomainEvent;

public sealed record LetterShipped(
    Guid LetterId,
    Guid CustomerId,
    string TrackingCode,
    decimal PointsToEarn,
    Guid? ReferrerCustomerId,
    decimal ReferralPointsToEarn) : DomainEvent;

public sealed record LetterDelivered(Guid LetterId, Guid CustomerId) : DomainEvent;

public sealed record LetterCancelledAfterPayment(Guid LetterId, Guid CustomerId, decimal RefundAmount, decimal PointsUsed) : DomainEvent;
