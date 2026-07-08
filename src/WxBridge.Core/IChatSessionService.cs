namespace WxBridge.Core;

public interface IChatSessionService
{
    OperationResult ListSessions();

    OperationResult InspectSessions(InspectSessionsRequest request);

    OperationResult SwitchSession(SwitchSessionRequest request);

    OperationResult SearchSession(SearchSessionRequest request);
}
