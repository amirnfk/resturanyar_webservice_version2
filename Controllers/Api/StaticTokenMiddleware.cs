namespace resturanyar.Controllers.Api
{
    public class StaticTokenMiddleware
    {
        private readonly RequestDelegate _next;
        private const string StaticToken = "stR@nG3_Stat1c_T0ken_Resturanyar_2025!#X9LpQ"; 

        public StaticTokenMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Authorization header missing");
                return;
            }

            var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");

            if (token != StaticToken)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Invalid token");
                return;
            }

            await _next(context);
        }
    }

}
