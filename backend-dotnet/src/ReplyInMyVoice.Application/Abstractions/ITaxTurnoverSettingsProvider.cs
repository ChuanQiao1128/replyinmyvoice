using ReplyInMyVoice.Application.Common;

namespace ReplyInMyVoice.Application.Abstractions;

public interface ITaxTurnoverSettingsProvider
{
    TaxTurnoverSettings GetSettings();
}
