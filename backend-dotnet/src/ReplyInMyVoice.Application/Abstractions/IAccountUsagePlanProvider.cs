using ReplyInMyVoice.Application.Common;
using ReplyInMyVoice.Domain.Entities;

namespace ReplyInMyVoice.Application.Abstractions;

public interface IAccountUsagePlanProvider
{
    AccountUsagePlanDto GetUsagePlan(AppUser user);
}
