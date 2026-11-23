using MoissonMontreal.Core.Authorization;
using MoissonMontreal.Core.Receipts;

namespace MoissonMontreal.Tests.Receipts;

public class ReceiptStateMachineTests
{
    private static readonly Dictionary<(ReceiptStatus From, ReceiptAction Action), ReceiptStatus> AgreedTransitions = new()
    {
        [(ReceiptStatus.Draft, ReceiptAction.Submit)] = ReceiptStatus.PendingSupplyApproval,
        [(ReceiptStatus.PendingSupplyApproval, ReceiptAction.ApproveLevel1)] = ReceiptStatus.PendingFinanceApproval,
        [(ReceiptStatus.PendingSupplyApproval, ReceiptAction.RefuseLevel1)] = ReceiptStatus.Draft,
        [(ReceiptStatus.PendingFinanceApproval, ReceiptAction.ApproveLevel2)] = ReceiptStatus.Approved,
        [(ReceiptStatus.PendingFinanceApproval, ReceiptAction.RefuseLevel2)] = ReceiptStatus.Draft,
        [(ReceiptStatus.Approved, ReceiptAction.Issue)] = ReceiptStatus.Issued,
        [(ReceiptStatus.Draft, ReceiptAction.Cancel)] = ReceiptStatus.Cancelled,
        [(ReceiptStatus.PendingSupplyApproval, ReceiptAction.Cancel)] = ReceiptStatus.Cancelled,
        [(ReceiptStatus.PendingFinanceApproval, ReceiptAction.Cancel)] = ReceiptStatus.Cancelled,
        [(ReceiptStatus.Approved, ReceiptAction.Cancel)] = ReceiptStatus.Cancelled,
        [(ReceiptStatus.Issued, ReceiptAction.Cancel)] = ReceiptStatus.Cancelled,
    };

    private static readonly Dictionary<ReceiptAction, string> AgreedPermissions = new()
    {
        [ReceiptAction.Submit] = Permissions.ManageReceipts,
        [ReceiptAction.ApproveLevel1] = Permissions.ApproveReceiptsLevel1,
        [ReceiptAction.RefuseLevel1] = Permissions.ApproveReceiptsLevel1,
        [ReceiptAction.ApproveLevel2] = Permissions.ApproveReceiptsLevel2,
        [ReceiptAction.RefuseLevel2] = Permissions.ApproveReceiptsLevel2,
        [ReceiptAction.Issue] = Permissions.ManageReceipts,
        [ReceiptAction.Cancel] = Permissions.ManageReceipts,
    };

    public static TheoryData<ReceiptStatus, ReceiptAction> EveryStatusAndAction()
    {
        var data = new TheoryData<ReceiptStatus, ReceiptAction>();
        foreach (var status in Enum.GetValues<ReceiptStatus>())
        {
            foreach (var action in Enum.GetValues<ReceiptAction>())
            {
                data.Add(status, action);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryStatusAndAction))]
    public void GetTransition_FollowsTheAgreedDiagram(ReceiptStatus status, ReceiptAction action)
    {
        if (AgreedTransitions.TryGetValue((status, action), out var expected))
        {
            Assert.Equal(expected, ReceiptStateMachine.GetTransition(status, action).To);
        }
        else
        {
            Assert.Throws<InvalidReceiptTransitionException>(() => ReceiptStateMachine.GetTransition(status, action));
        }
    }

    [Fact]
    public void Transitions_ContainOnlyTheAgreedTransitions()
    {
        Assert.Equivalent(
            AgreedTransitions.Select(t => (t.Key.From, t.Key.Action, To: t.Value)),
            ReceiptStateMachine.Transitions.Select(t => (t.From, t.Action, t.To)),
            strict: true);
    }

    [Fact]
    public void Transitions_RequireThePermissionOfTheirAction()
    {
        Assert.All(ReceiptStateMachine.Transitions, t => Assert.Equal(AgreedPermissions[t.Action], t.Permission));
    }

    [Fact]
    public void Transitions_RequireAReason_OnlyToRefuseOrCancel()
    {
        ReceiptAction[] withReason = [ReceiptAction.RefuseLevel1, ReceiptAction.RefuseLevel2, ReceiptAction.Cancel];

        Assert.All(ReceiptStateMachine.Transitions, t => Assert.Equal(withReason.Contains(t.Action), t.RequiresReason));
    }

    [Fact]
    public void Transitions_ForbidTheSubmitter_OnlyToApprove()
    {
        ReceiptAction[] approvals = [ReceiptAction.ApproveLevel1, ReceiptAction.ApproveLevel2];

        Assert.All(ReceiptStateMachine.Transitions, t => Assert.Equal(approvals.Contains(t.Action), t.ForbiddenToSubmitter));
    }

    [Fact]
    public void AllowedActions_IsEmpty_WhenTheReceiptIsCancelled()
    {
        Assert.Empty(ReceiptStateMachine.AllowedActions(ReceiptStatus.Cancelled));
    }

    [Fact]
    public void GetTransition_ExplainsWhatIsAllowed_WhenTheActionIsRefused()
    {
        var error = Assert.Throws<InvalidReceiptTransitionException>(
            () => ReceiptStateMachine.GetTransition(ReceiptStatus.Draft, ReceiptAction.ApproveLevel1));

        Assert.Equal("ApproveLevel1 is not allowed on a receipt in status Draft. Allowed actions: Submit, Cancel.", error.Message);
        Assert.Equal([ReceiptAction.Submit, ReceiptAction.Cancel], error.AllowedActions);
    }

    [Fact]
    public void GetTransition_SaysNothingIsPossible_WhenTheReceiptIsCancelled()
    {
        var error = Assert.Throws<InvalidReceiptTransitionException>(
            () => ReceiptStateMachine.GetTransition(ReceiptStatus.Cancelled, ReceiptAction.Submit));

        Assert.Equal("Submit is not allowed on a receipt in status Cancelled: no action is possible in this status.", error.Message);
    }
}
