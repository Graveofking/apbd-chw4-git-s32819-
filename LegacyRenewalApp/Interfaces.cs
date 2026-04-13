namespace LegacyRenewalApp
{
    public interface ICustomerRepository
    {
        Customer GetById(int customerId);
    }

    public interface ISubscriptionPlanRepository
    {
        SubscriptionPlan GetByCode(string code);
    }

    public interface IBillingGateway
    {
        void SaveInvoice(RenewalInvoice invoice);
        void SendEmail(string email, string subject, string body);
    }
}
