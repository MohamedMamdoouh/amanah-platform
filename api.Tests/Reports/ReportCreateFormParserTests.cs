using System.Text;
using System.Text.Json;
using Amanah.Api.Models.Common;
using Amanah.Api.Services.Reports;
using Amanah.Api.Validators.Reports;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Amanah.Api.Tests.Reports;

public class ReportCreateFormParserTests
{
    [Fact]
    public async Task Parse_reads_report_json_from_file_part()
    {
        var request = TestReportHelpers.BuildValidLostRequest();
        var json = JsonSerializer.Serialize(request, ApiJson.SerializerOptions);
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var reportFile = new FormFile(stream, 0, stream.Length, "report", "blob")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/json",
        };

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "multipart/form-data";
        httpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues>(),
            new FormFileCollection { reportFile });

        var parser = new ReportCreateFormParser(new CreateReportRequestValidator());
        var parsed = await parser.ParseAsync(httpContext.Request);

        Assert.True(parsed.IsSuccess);
        Assert.Equal("lost", parsed.Value!.Request.Type);
        Assert.Equal("phones", parsed.Value.Request.CategoryCode);
        Assert.Empty(parsed.Value.Photos);
    }

    [Fact]
    public async Task Parse_reads_report_json_from_form_field()
    {
        var request = TestReportHelpers.BuildValidLostRequest();
        var json = JsonSerializer.Serialize(request, ApiJson.SerializerOptions);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "multipart/form-data";
        httpContext.Request.Form = new FormCollection(
            new Dictionary<string, StringValues>
            {
                ["report"] = json,
            });

        var parser = new ReportCreateFormParser(new CreateReportRequestValidator());
        var parsed = await parser.ParseAsync(httpContext.Request);

        Assert.True(parsed.IsSuccess);
        Assert.Equal("lost", parsed.Value!.Request.Type);
    }
}
