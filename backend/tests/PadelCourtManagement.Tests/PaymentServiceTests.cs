using PadelCourtManagement.Application;
using PadelCourtManagement.Domain;

namespace PadelCourtManagement.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task ActiveMemberCanPayParticipantPlace()
    {
        var repository = new FakePaymentRepository();
        var service = new PaymentService(repository);

        var result = await service.PayParticipantAsync(7, "g0001", CancellationToken.None);

        Assert.Equal(7, result.MatchId);
        Assert.Equal(15.00m, result.TotalAmount);
        Assert.Equal(1, repository.Calls);
    }

    [Fact]
    public async Task InactiveMemberCannotPay()
    {
        var service = new PaymentService(new FakePaymentRepository
        {
            Member = new ReservationMember(1, MembershipCategory.Global, null, false)
        });

        await Assert.ThrowsAsync<ReservationForbiddenException>(() =>
            service.PayParticipantAsync(7, "G0001", CancellationToken.None));
    }

    [Fact]
    public async Task Failed_payment_is_forwarded_without_confirming_the_place()
    {
        var repository = new FakePaymentRepository();
        var service = new PaymentService(repository);

        var result = await service.PayParticipantAsync(
            7,
            "G0001",
            CancellationToken.None,
            PaymentOutcome.Failed);

        Assert.Equal(PaymentOutcome.Failed, result.Outcome);
        Assert.Equal(PaymentOutcome.Failed, repository.LastOutcome);
    }

    private sealed class FakePaymentRepository : IPaymentRepository
    {
        public ReservationMember? Member { get; init; } = new(1, MembershipCategory.Global, null, true);
        public int Calls { get; private set; }
        public PaymentOutcome LastOutcome { get; private set; }

        public Task<ReservationMember?> GetMemberAsync(string matricule, CancellationToken cancellationToken) =>
            Task.FromResult(Member);

        public Task<PaymentResult> PayParticipantAsync(
            int matchId,
            int memberId,
            DateTime paidAt,
            CancellationToken cancellationToken,
            PaymentOutcome outcome = PaymentOutcome.Succeeded)
        {
            Calls++;
            LastOutcome = outcome;
            return Task.FromResult(new PaymentResult(2, matchId, 3, 15.00m, 0m, 15.00m, outcome));
        }
    }
}
