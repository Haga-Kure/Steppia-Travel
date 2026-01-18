namespace Travel.Api.Dtos;

public record CreatePaymentRequest(
    string BookingCode,
    string Provider // "qpay" | "stripe" | "manual"
);

public record CreatePaymentResponse(
    string PaymentId,
    string Status,
    string Provider,
    string? InvoiceId,
    string? CheckoutUrl,
    string? QrText
);
