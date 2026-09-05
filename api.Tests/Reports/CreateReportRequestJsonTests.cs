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
    public void Deserialize_accepts_reward_amount_as_json_string()
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
              "rewardAmount": "250",
              "hiddenDetail": "Contains a photo of my family inside."
            }
            """);

        Assert.True(request.HasReward);
        Assert.Equal(250, request.RewardAmount);
    }

    [Fact]
    public void Deserialize_accepts_integer_category_field_as_json_number()
    {
        var request = Deserialize(
            """
            {
              "type": "lost",
              "categoryCode": "keys",
              "title": "Lost house keys with a blue tag",
              "description": "I lost my keys near Ramses station yesterday evening.",
              "dateLostOrFound": "2026-09-05",
              "governorateCode": "cairo",
              "hasReward": false,
              "hiddenDetail": "The blue tag has my first name written on it.",
              "categoryFields": {
                "key_type": "house",
                "key_count": 3
              }
            }
            """);

        Assert.Equal("house", request.CategoryFields["key_type"]);
        Assert.Equal("3", request.CategoryFields["key_count"]);
    }

    [Fact]
    public void Deserialize_accepts_integer_category_field_as_json_string()
    {
        var request = Deserialize(
            """
            {
              "type": "lost",
              "categoryCode": "keys",
              "title": "Lost house keys with a blue tag",
              "description": "I lost my keys near Ramses station yesterday evening.",
              "dateLostOrFound": "2026-09-05",
              "governorateCode": "cairo",
              "hasReward": false,
              "hiddenDetail": "The blue tag has my first name written on it.",
              "categoryFields": {
                "key_type": "house",
                "key_count": "3"
              }
            }
            """);

        Assert.Equal("house", request.CategoryFields["key_type"]);
        Assert.Equal("3", request.CategoryFields["key_count"]);
    }

    private static CreateReportRequest Deserialize(string json) =>
        JsonSerializer.Deserialize<CreateReportRequest>(json, ApiJson.SerializerOptions)
        ?? throw new InvalidOperationException("Report JSON deserialized to null.");
}
