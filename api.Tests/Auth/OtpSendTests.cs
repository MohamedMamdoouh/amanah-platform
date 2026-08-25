using Amanah.Api.Data;

using Amanah.Api.Data.Entities;

using Amanah.Api.Models.Errors;

using Amanah.Api.Tests.Infrastructure;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.DependencyInjection;



namespace Amanah.Api.Tests.Auth;



public class OtpSendTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory>

{

    [Fact]

    public async Task Valid_phone_and_captcha_returns_204_and_creates_otp_row()

    {

        await using var context = await CreateContextAsync();



        var response = await context.SendOtpAsync("01012345678");



        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        await context.WaitForSmsCountAsync(1);

        Assert.Equal("+201012345678", context.SmsSender.SentMessages[0].Phone);



        var otpCount = await context.DbContext.OtpCodes.CountAsync(code => code.Phone == "+201012345678");

        Assert.Equal(1, otpCount);

    }



    [Fact]

    public async Task Invalid_phone_returns_400_without_creating_otp_or_sending_sms()

    {

        await using var context = await CreateContextAsync();



        var response = await context.SendOtpAsync("12345");

        var error = await context.ReadErrorAsync(response);



        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(ErrorCodes.ValidationFailed, error?.Code);
        Assert.NotNull(error?.Errors?["phone"]);

        Assert.Empty(context.SmsSender.SentMessages);

        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());

    }



    [Fact]

    public async Task Failed_captcha_returns_400_without_creating_otp_or_sending_sms()

    {

        await using var context = await CreateContextAsync();

        context.CaptchaVerifier.ShouldSucceed = false;



        var response = await context.SendOtpAsync("01012345678");

        var error = await context.ReadErrorAsync(response);



        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);

        Assert.Equal(ErrorCodes.CaptchaFailed, error?.Code);

        Assert.Empty(context.SmsSender.SentMessages);

        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());

    }



    [Fact]

    public async Task Resend_within_cooldown_returns_429_with_retry_after_and_no_sms()

    {

        await using var context = await CreateContextAsync();



        var firstResponse = await context.SendOtpAsync("01012345678");

        Assert.Equal(System.Net.HttpStatusCode.NoContent, firstResponse.StatusCode);

        await context.WaitForSmsCountAsync(1);



        context.SmsSender.SentMessages.Clear();



        var secondResponse = await context.SendOtpAsync("01012345678");

        var error = await context.ReadErrorAsync(secondResponse);



        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, secondResponse.StatusCode);

        Assert.Equal(ErrorCodes.OtpCooldown, error?.Code);

        Assert.True(secondResponse.Headers.RetryAfter is not null);

        Assert.Empty(context.SmsSender.SentMessages);

    }



    [Fact]

    public async Task Fourth_send_in_same_cairo_day_returns_429_daily_limit_without_sms()

    {

        await using var context = await CreateContextAsync();

        const string phone = "+201012345678";

        var now = DateTimeOffset.UtcNow;



        for (var index = 0; index < 3; index++)

        {

            context.DbContext.OtpSmsOutboxMessages.Add(new OtpSmsOutboxMessage

            {

                Id = Guid.NewGuid(),

                Phone = phone,

                ProtectedPayload = $"payload-{index}",

                Status = OtpSmsOutboxStatus.Sent,

                CreatedAt = now.AddMinutes(-150 - (index * 5)),

                ProcessedAt = now.AddMinutes(-150 - (index * 5)),

            });

        }



        await context.DbContext.SaveChangesAsync();

        context.DbContext.ChangeTracker.Clear();



        var response = await context.SendOtpAsync("01012345678");

        var error = await context.ReadErrorAsync(response);



        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);

        Assert.Equal(ErrorCodes.OtpDailyLimit, error?.Code);

        Assert.True(response.Headers.RetryAfter is not null);

        Assert.Empty(context.SmsSender.SentMessages);

        Assert.Equal(3, await context.DbContext.OtpSmsOutboxMessages.CountAsync(

            message => message.Phone == phone && message.Status == OtpSmsOutboxStatus.Sent));

    }



    [Fact]

    public async Task Third_send_in_rolling_hour_returns_429_hourly_limit_without_sms()

    {

        await using var context = await CreateContextAsync();

        const string phone = "+201012345678";

        var now = DateTimeOffset.UtcNow;



        context.DbContext.OtpSmsOutboxMessages.Add(new OtpSmsOutboxMessage

        {

            Id = Guid.NewGuid(),

            Phone = phone,

            ProtectedPayload = "payload-1",

            Status = OtpSmsOutboxStatus.Sent,

            CreatedAt = now.AddMinutes(-30),

            ProcessedAt = now.AddMinutes(-30),

        });

        context.DbContext.OtpSmsOutboxMessages.Add(new OtpSmsOutboxMessage

        {

            Id = Guid.NewGuid(),

            Phone = phone,

            ProtectedPayload = "payload-2",

            Status = OtpSmsOutboxStatus.Sent,

            CreatedAt = now.AddMinutes(-20),

            ProcessedAt = now.AddMinutes(-20),

        });



        await context.DbContext.SaveChangesAsync();

        context.DbContext.ChangeTracker.Clear();



        var response = await context.SendOtpAsync("01012345678");

        var error = await context.ReadErrorAsync(response);



        Assert.Equal(System.Net.HttpStatusCode.TooManyRequests, response.StatusCode);

        Assert.Equal(ErrorCodes.OtpHourlyLimit, error?.Code);

        Assert.True(response.Headers.RetryAfter is not null);

        Assert.Empty(context.SmsSender.SentMessages);

        Assert.Equal(2, await context.DbContext.OtpSmsOutboxMessages.CountAsync(

            message => message.Phone == phone && message.Status == OtpSmsOutboxStatus.Sent));

    }



    [Fact]

    public async Task Sms_provider_failure_eventually_marks_outbox_failed_and_removes_otp()

    {

        await using var context = await CreateContextAsync();

        context.SmsSender.ShouldThrow = true;



        var response = await context.SendOtpAsync("01012345678");



        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        await context.WaitForOutboxStatusAsync(OtpSmsOutboxStatus.Failed);

        Assert.Equal(0, await context.DbContext.OtpCodes.CountAsync());

        Assert.Empty(context.SmsSender.SentMessages);

    }



    [Fact]

    public async Task Sms_provider_timeout_keeps_otp_pending_for_worker_retry()

    {

        await using var context = await CreateContextAsync();

        context.SmsSender.ShouldTimeout = true;



        var response = await context.SendOtpAsync("01012345678");



        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        await context.WaitForOutboxStatusAsync(OtpSmsOutboxStatus.Pending);

        Assert.Equal(1, await context.DbContext.OtpCodes.CountAsync());

        Assert.Empty(context.SmsSender.SentMessages);



        context.SmsSender.ShouldTimeout = false;

        await context.WaitForSmsCountAsync(1);

        await context.WaitForOutboxStatusAsync(OtpSmsOutboxStatus.Sent);

    }



    [Fact]

    public async Task Dispatch_retry_uses_idempotency_key_without_duplicate_sms()

    {

        await using var context = await CreateContextAsync();



        var response = await context.SendOtpAsync("01012345678");

        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        await context.WaitForSmsCountAsync(1);



        var outboxId = await context.DbContext.OtpSmsOutboxMessages

            .Where(message => message.Status == OtpSmsOutboxStatus.Sent)

            .Select(message => message.Id)

            .SingleAsync();



        var sentAgain = await context.Dispatcher.DispatchAsync(outboxId);



        Assert.True(sentAgain);

        Assert.Single(context.SmsSender.SentMessages);

    }



    [Fact]

    public async Task Arabic_indic_digits_are_normalized_and_accepted()

    {

        await using var context = await CreateContextAsync();



        var response = await context.SendOtpAsync("٠١٠١٢٣٤٥٦٧٨");



        Assert.Equal(System.Net.HttpStatusCode.NoContent, response.StatusCode);

        await context.WaitForSmsCountAsync(1);

        Assert.Equal("+201012345678", context.SmsSender.SentMessages[0].Phone);

    }



    private async Task<OtpSendTestContext> CreateContextAsync()

    {

        factory.CaptchaVerifier.ShouldSucceed = true;

        factory.SmsSender.ShouldThrow = false;

        factory.SmsSender.ShouldTimeout = false;

        factory.SmsSender.SentMessages.Clear();



        await using var setupScope = factory.Services.CreateAsyncScope();

        var setupContext = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

        await setupContext.Database.MigrateAsync();

        await setupContext.OtpCodes.ExecuteDeleteAsync();

        await setupContext.OtpSmsOutboxMessages.ExecuteDeleteAsync();



        var scope = factory.Services.CreateAsyncScope();

        return new OtpSendTestContext(

            factory.CreateClient(),

            factory.SmsSender,

            factory.CaptchaVerifier,

            scope);

    }

}


