using Ez.Bank.Core;
using Microsoft.Azure.Functions.Worker;

namespace Ez.Bank.FunctionsApi.Extensions;

public static class FunctionContextExtensions
{
    public static CallerContext GetCallerContext(this FunctionContext context)
    {
        return (CallerContext)context.Items["CallerContext"];
    }
}
