using EchoForge.Core.DTOs;
using EchoForge.Core.Models;

namespace EchoForge.Tests;

public class ModelTests
{
    // ─── ProjectStatus Tests ───

    [Fact]
    public void ProjectStatus_DefaultIsCreated()
    {
        var project = new Project();
        Assert.Equal(ProjectStatus.Created, project.Status);
    }

    [Fact]
    public void ProjectStatus_HasAllExpectedValues()
    {
        var values = Enum.GetValues<ProjectStatus>();
        Assert.Contains(ProjectStatus.Created, values);
        Assert.Contains(ProjectStatus.Analyzing, values);
        Assert.Contains(ProjectStatus.GeneratingImages, values);
        Assert.Contains(ProjectStatus.ComposingVideo, values);
        Assert.Contains(ProjectStatus.GeneratingSEO, values);
        Assert.Contains(ProjectStatus.AwaitingApproval, values);
        Assert.Contains(ProjectStatus.Uploading, values);
        Assert.Contains(ProjectStatus.Completed, values);
        Assert.Contains(ProjectStatus.Failed, values);
        Assert.Contains(ProjectStatus.ReviewingScenes, values);
    }

    // ─── Project Model Tests ───

    [Fact]
    public void Project_DefaultValues_AreCorrect()
    {
        var project = new Project();

        Assert.Equal(string.Empty, project.Title);
        Assert.Equal(string.Empty, project.AudioPath);
        Assert.Equal(FormatType.Vertical_9x16, project.FormatType);
        Assert.False(project.ExtractAutoShorts);
        Assert.Equal("flux", project.ImageModel);
        Assert.Equal(8, project.UniqueImageCount);
        Assert.Equal("private", project.PrivacyStatus);
    }

    [Fact]
    public void Project_CanSetProperties()
    {
        var project = new Project
        {
            Title = "Test Song",
            AudioPath = "/audio/test.mp3",
            BPM = 128.0,
            Duration = 240.0,
            SceneCount = 8,
            ImageModel = "turbo",
            UniqueImageCount = 12,
            ImageStyle = "cyberpunk",
            TransitionStyle = "fade",
            PrivacyStatus = "public"
        };

        Assert.Equal("Test Song", project.Title);
        Assert.Equal(128.0, project.BPM);
        Assert.Equal(240.0, project.Duration);
        Assert.Equal(8, project.SceneCount);
        Assert.Equal("turbo", project.ImageModel);
        Assert.Equal(12, project.UniqueImageCount);
        Assert.Equal("cyberpunk", project.ImageStyle);
        Assert.Equal("fade", project.TransitionStyle);
        Assert.Equal("public", project.PrivacyStatus);
    }

    // ─── TimelineItemDto Tests ───

    [Fact]
    public void TimelineItemDto_DurationStr_FormatsCorrectly()
    {
        var item = new TimelineItemDto { Duration = 5.75 };
        Assert.Equal("5.75s", item.DurationStr);
    }

    [Fact]
    public void TimelineItemDto_DefaultValues()
    {
        var item = new TimelineItemDto();
        Assert.Equal(0, item.Duration);
        Assert.Equal(1.0, item.Speed);
        Assert.Equal("none", item.Filter);
        Assert.Equal(0, item.FadeInDuration);
        Assert.Equal(0, item.FadeOutDuration);
        Assert.False(item.HasFadeIn);
        Assert.False(item.HasFadeOut);
        Assert.False(item.IsSelected);
    }

    [Fact]
    public void TimelineItemDto_HasFadeIn_WhenDurationSet()
    {
        var item = new TimelineItemDto { FadeInDuration = 1.0 };
        Assert.True(item.HasFadeIn);
        Assert.False(item.HasFadeOut);
    }

    [Fact]
    public void TimelineItemDto_HasFadeOut_WhenDurationSet()
    {
        var item = new TimelineItemDto { FadeOutDuration = 2.0 };
        Assert.False(item.HasFadeIn);
        Assert.True(item.HasFadeOut);
    }

    [Fact]
    public void TimelineItemDto_PropertyChanged_FiresOnDurationChange()
    {
        var item = new TimelineItemDto();
        var changedProperties = new List<string>();
        item.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        item.Duration = 10.0;

        Assert.Contains("Duration", changedProperties);
        Assert.Contains("DurationStr", changedProperties);
    }

    [Fact]
    public void TimelineItemDto_PropertyChanged_DoesNotFireWhenSameValue()
    {
        var item = new TimelineItemDto { Duration = 5.0 };
        var changedProperties = new List<string>();
        item.PropertyChanged += (s, e) => changedProperties.Add(e.PropertyName!);

        item.Duration = 5.0; // Same value, should not fire

        Assert.Empty(changedProperties);
    }

    // ─── ProjectDto Tests ───

    [Fact]
    public void ProjectDto_Scenes_IsInitialized()
    {
        var dto = new ProjectDto();
        Assert.NotNull(dto.Scenes);
        Assert.Empty(dto.Scenes);
    }

    [Fact]
    public void ProjectDto_DefaultValues()
    {
        var dto = new ProjectDto();
        Assert.Equal("flux", dto.ImageModel);
        Assert.Equal(8, dto.UniqueImageCount);
        Assert.Equal("private", dto.PrivacyStatus);
        Assert.Equal(string.Empty, dto.Title);
    }

    // ─── FormatType Tests ───

    [Fact]
    public void FormatType_HasVerticalFormat()
    {
        Assert.Equal(FormatType.Vertical_9x16, (FormatType)0);
    }
}