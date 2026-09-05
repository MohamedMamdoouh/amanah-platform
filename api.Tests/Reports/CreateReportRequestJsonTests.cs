using System.Text.Json;
using Amanah.Api.Models.Common;
using Amanah.Contracts.Requests.Reports;

namespace Amanah.Api.Tests.Reports;

public class CreateReportRequestJsonTests
{
    [Fact]
    public void Deserialize_accepts_reward_amount_as_json_number()
    {
        var request = Deserialize(
            """
            {
              "type": "lost",
              "categoryCode": "phones",
              "title": "Lost black iPhone",
              "description": "I lost my phone near Ramses station yesterday evening.",
              "dateLostOrFound": "2026-09-05",
              "governorateCode": "cairo",
              "hasReward": true,
              "rewardAmount": 250,
              "hiddenDetail": "Contains a photo of my family inside."
            }
            """);

        Assert.True(request.HasReward);
        Assert.Equal(250, request.RewardAmount);
    }

    [Fact]
    public void Deserialize_rejects_reward_amount_as_json_string()
    {
        Assert.Throws<JsonException>(() => Deserialize(
            """
            {
              "type": "lost",
              "categoryCode": "phones",
              "title": "Lost black iPhone",
              "description": "I lost my phone near Ramses station yesterday evening.",
              "dateLostOrFound": "2026-09-05",
              "governorateCode": "cairo",
              "hasReward": true,
              "rewardAmount": "250",
              "hiddenDetail": "Contains a photo of my family inside."
            }
            """));
    }

    private static CreateReportRequest Deserialize(string json) =>
        JsonSerializer.Deserialize<CreateReportRequest>(json, ApiJson.SerializerOptions)
        ?? throw new InvalidOperationException("Report JSON deserialized to null.");
}
