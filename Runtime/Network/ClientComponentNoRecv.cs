namespace UNetwork
{
    /// <summary>
    /// 客户端的 业务逻辑 管理类
    /// </summary>
    public class ClientComponentNoRecv : ClientComponent
    {
        public override void Connect()
        {
            base.Connect();
            ((TChannel)Service.GetChannel()).NoRecv = true;
        }
    }
}