namespace EasyRedmineTool.Core.Services;

using EasyRedmineTool.Core;
using EasyRedmineTool.Core.Api;
using EasyRedmineTool.Core.Configuration;
using EasyRedmineTool.Core.Models.TimeEntries;
using EasyRedmineTool.Core.Services.Interfaces;

public static class TimeEntryCustomFieldDefinitionFactory
{
    public static TimeEntryCustomFieldDefinitionDto CreateProbedDefinition(
        int id,
        string name,
        int? activityId) =>
        new()
        {
            Id = id,
            Name = name,
            FieldFormat = "depending_enumeration",
            IsRequired = true,
            IsProjectScoped = true,
            ActivityIds = activityId.HasValue ? [activityId.Value] : [],
            PossibleValues = []
        };
}
