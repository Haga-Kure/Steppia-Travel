namespace Travel.Api.Dtos;

public record PaymentWebhookRequest(
    string Provider,
    string InvoiceId,
    string Status // "paid" | "failed"
);
