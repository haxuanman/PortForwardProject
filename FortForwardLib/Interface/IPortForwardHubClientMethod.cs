namespace FortForwardLib.Interface
{
    public interface IPortForwardHubClientMethod
    {

        /// <summary>
        /// function nhận data từ host cho client
        /// </summary>
        /// <param name="fromUserName"></param>
        /// <param name="toUserName"></param>
        /// <param name="sessionId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        Task SendDatasync(string fromUserName, string toUserName, Guid sessionId, string data);

        /// <summary>
        /// tạo phiên kết nối
        /// </summary>
        /// <param name="fromUserName"></param>
        /// <param name="toUserName"></param>
        /// <returns></returns>
        Task CreateSessionAsync(string fromUserName, string toUserName, Guid sessionId, int hostPort);

        /// <summary>
        /// đóng phiên kết nối
        /// </summary>
        /// <param name="fromUserName"></param>
        /// <param name="toUserName"></param>
        /// <param name="sessionId"></param>
        /// <returns></returns>
        Task DeleteSessionAsync(string fromUserName, string toUserName, Guid sessionId);

    }
}
