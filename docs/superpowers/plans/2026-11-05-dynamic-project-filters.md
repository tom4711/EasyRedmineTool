# Dynamic Project Filters Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the team lead filter the dashboard's project checklist by project metadata Redmine actually returns, while retaining selected projects and only loading tickets for selected projects.

**Architecture:** Extend the project DTO with standard metadata, normalize standard and custom fields into field/value options in the dashboard view model, and persist the selected filter alongside existing settings. The Avalonia view gets field and value selectors; filtering remains local and does not call ticket APIs.

**Tech Stack:** .NET 10, C#, CommunityToolkit.Mvvm, Avalonia XAML, existing JSON settings service.

**Spec:** `docs/superpowers/specs/2026-11-05-dynamic-project-filters-design.md`

## Global Constraints

- Show only metadata and values actually returned by Redmine.
- Project filters affect only the visible project checklist.
- Selected project IDs survive filtering and remain selected when revealed again.
- Fetch tickets only for selected projects; do not write changes to Redmine.
- Persist the filter locally across app restarts.

## Review Focus

- A custom field missing from Redmine's response is omitted from filter choices rather than fabricated.
- Null/empty metadata does not create a selectable blank value.
- Duplicate values from multiple projects appear only once in a value selector.
- Changing/clearing the filter does not clear project selections.
- Loading projects without custom fields still provides available standard metadata filters.

---

### Task 1: Model Redmine project metadata

**Files:**
- Modify: `src/EasyRedmineTool.Core/Models/Tickets/ProjectDto.cs`
- Modify: `src/EasyRedmineTool.Core/Models/Tickets/ProjectListResponse.cs` only if a nested parent DTO is needed

**Interfaces:**
- Produces project properties for identifier, public visibility, parent project, status, and existing custom field values.
- Custom-field values can be normalized to display strings for filter option generation; booleans display as `ja`/`nein`, arrays as joined values, and null/undefined values are omitted.

- [x] Add DTO properties using Redmine's JSON names and types, preserving compatibility when fields are absent.
- [x] Add a scalar-to-display-value helper for custom fields.
- [x] Build the Desktop project and confirm API model compilation.

### Task 2: Discover and apply metadata filters in the view model

**Files:**
- Modify: `src/EasyRedmineTool.Core/ViewModels/KanbanDashboardViewModel.cs`
- Modify: `src/EasyRedmineTool.Core/Configuration/AppSettings.cs`
- Modify: `src/EasyRedmineTool.Core/Services/AppSettingsService.cs`

**Interfaces:**
- Expose observable field choices and distinct value choices for the selected field.
- Persist selected field and value as nullable strings in `AppSettings`.
- Keep the full project collection separate from its filtered presentation so selected IDs are not lost.

- [x] Build standard and custom metadata fields from loaded projects, skipping fields without values.
- [x] Apply exact field/value matching only to the displayed project checklist.
- [x] On field change, rebuild values and clear a now-invalid selected value; on clear, show all projects.
- [x] Keep selection persistence and ticket loading tied to selected project IDs, independent of the active metadata filter.
- [x] Copy the new settings properties in the settings clone/update path and preserve defaults for existing settings files.
- [x] Build the Desktop project and check for compiler/XAML binding errors.

### Task 3: Add filter controls to the dashboard

**Files:**
- Modify: `src/EasyRedmineTool.Desktop/Views/KanbanDashboardView.axaml`

**Interfaces:**
- Bind a project-field selector and a value selector to the view model's dynamic options.
- Provide a clear-filter action and a result count for the visible projects.

- [x] Replace the fixed `Geschlossen` checkbox with compact dynamic field/value selectors and a clear action.
- [x] Keep project checkboxes bound to the filtered presentation while preserving their selected state.
- [x] Build the Desktop project and inspect the changed UI bindings in the compiler output.
