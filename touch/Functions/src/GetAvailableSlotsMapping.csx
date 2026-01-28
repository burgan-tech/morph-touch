using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BBT.Workflow.Scripting;
using BBT.Workflow.Definitions;

/// <summary>
/// Mapping for GetAvailableSlotsTask (DaprServiceTask - Type 3)
/// Queries absence-entry workflow via Dapr service invocation to get public holidays, 
/// working hours changes, and personal leaves.
/// Then calculates available appointment slots for the given advisor and date.
/// 
/// Reference: https://github.com/burgan-tech/vnext-runtime/blob/main/doc/en/flow/tasks/dapr-service.md
/// </summary>
public class GetAvailableSlotsMapping : ScriptBase, IMapping
{
    private const int DefaultAppointmentDuration = 30; // minutes

    public Task<ScriptResponse> InputHandler(WorkflowTask task, ScriptContext context)
    {
        try
        {
            var serviceTask = task as DaprServiceTask;
            if (serviceTask == null)
                throw new InvalidOperationException("Task must be a DaprServiceTask");

            // Read parameters from query string (GET request)
            var advisorId = context.QueryParameters?["advisorId"]?.ToString();
            var date = context.QueryParameters?["date"]?.ToString();
            var durationStr = context.QueryParameters?["duration"]?.ToString();

            if (string.IsNullOrEmpty(advisorId) || string.IsNullOrEmpty(date))
            {
                return Task.FromResult(new ScriptResponse
                {
                    Key = "validation-error",
                    Data = new { 
                        error = "advisorId and date are required",
                        errorCode = "VALIDATION_ERROR"
                    }
                });
            }

            // Configure Dapr Service Task
            // AppId is the target service name in Dapr
            serviceTask.SetAppId("vnext-app");
            
            
            // Build query string for filtering
            var queryParams = new List<string>
            {
                "status=Active",
                "currentState=scheduled,active",
                "pageSize=100"
            };
            serviceTask.SetQueryString(string.Join("&", queryParams));

            return Task.FromResult(new ScriptResponse());
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ScriptResponse
            {
                Key = "input-error",
                Data = new { 
                    error = ex.Message,
                    errorCode = "INPUT_ERROR"
                }
            });
        }
    }

    public async Task<ScriptResponse> OutputHandler(ScriptContext context)
    {
        try
        {
            // Read parameters from query string (GET request)
            var advisorId = context.QueryParameters?["advisorId"]?.ToString();
            var dateStr = context.QueryParameters?["date"]?.ToString();
            var durationStr = context.QueryParameters?["duration"]?.ToString();
            var duration = string.IsNullOrEmpty(durationStr) ? DefaultAppointmentDuration : int.Parse(durationStr);

            // Validation
            if (string.IsNullOrEmpty(advisorId) || string.IsNullOrEmpty(dateStr))
            {
                return new ScriptResponse
                {
                    Key = "validation-error",
                    Data = new
                    {
                        error = "advisorId and date are required",
                        errorCode = "VALIDATION_ERROR",
                        advisorId,
                        date = dateStr
                    }
                };
            }

            // Parse date - expecting "yyyy-MM-dd" format
            var requestedDate = DateTime.Parse(dateStr);
            var dayOfWeek = requestedDate.DayOfWeek.ToString().ToLower();

            // Get Dapr Service response
            var response = context.Body;
            
            // Check if service call was successful
            if (response?.isSuccess == false)
            {
                var errorType = ClassifyServiceError(response?.errorMessage?.ToString() ?? "");
                
                return new ScriptResponse
                {
                    Key = "service-error",
                    Data = new
                    {
                        error = "Failed to fetch absence entries",
                        errorCode = "SERVICE_ERROR",
                        errorType = errorType,
                        errorMessage = response?.errorMessage?.ToString(),
                        serviceInfo = new
                        {
                            appId = response?.metadata?.appId?.ToString(),
                            methodName = response?.metadata?.methodName?.ToString(),
                            httpVerb = response?.metadata?.httpVerb?.ToString()
                        }
                    },
                    Tags = new[] { "appointments", "error" }
                };
            }

            // Get items from service response
            // Response structure: { data: { items: [...] }, isSuccess: true, ... }
            var responseData = response?.data;
            var items = responseData?.items;
            
            var instances = new List<dynamic>();
            if (items != null)
            {
                foreach (var item in items)
                {
                    instances.Add(item);
                }
            }

            // Categorize instances
            var publicHolidays = new List<dynamic>();
            var workingHoursChanges = new List<dynamic>();
            var personalLeaves = new List<dynamic>();

            foreach (var instance in instances)
            {
                // Instance structure: { attributes: { absenceType, advisor, ... } }
                var attributes = instance?.attributes;
                if (attributes == null) continue;

                var absenceType = attributes?.absenceType?.ToString();
                var scope = attributes?.scope?.ToString();
                var advisor = attributes?.advisor?.ToString();

                switch (absenceType)
                {
                    case "public-holiday" when scope == "all-advisors":
                        publicHolidays.Add(instance);
                        break;
                    case "working-hours-change" when advisor == advisorId:
                        workingHoursChanges.Add(instance);
                        break;
                    case "personal-leave" when advisor == advisorId:
                        personalLeaves.Add(instance);
                        break;
                }
            }

            // Check for public holiday
            var holiday = FindPublicHoliday(publicHolidays, requestedDate);
            if (holiday != null)
            {
                return new ScriptResponse
                {
                    Key = "public-holiday",
                    Data = new
                    {
                        advisorId,
                        date = dateStr,
                        appointmentDuration = duration,
                        workingHours = new List<object>(),
                        availableSlots = new List<string>(),
                        isPublicHoliday = true,
                        publicHolidayName = holiday.title?.ToString() ?? "Public Holiday"
                    },
                    Tags = new[] { "appointments", "holiday" }
                };
            }

            // Get working hours (custom or default from config)
            var workingHours = GetWorkingHoursForDay(workingHoursChanges, dayOfWeek, requestedDate, context);

            if (workingHours == null || workingHours.Count == 0)
            {
                return new ScriptResponse
                {
                    Key = "no-working-hours",
                    Data = new
                    {
                        advisorId,
                        date = dateStr,
                        appointmentDuration = duration,
                        workingHours = new List<object>(),
                        availableSlots = new List<string>(),
                        isPublicHoliday = false
                    },
                    Tags = new[] { "appointments", "closed" }
                };
            }

            // Get unavailable time ranges from personal leaves
            var unavailableRanges = GetUnavailableRanges(personalLeaves, requestedDate);

            // Calculate available slots
            var availableSlots = CalculateAvailableSlots(workingHours, unavailableRanges, duration);

            return new ScriptResponse
            {
                Key = "available-slots-success",
                Data = new
                {
                    advisorId,
                    date = dateStr,
                    appointmentDuration = duration,
                    workingHours,
                    availableSlots,
                    isPublicHoliday = false
                },
                Tags = new[] { "appointments", "availability", "success" }
            };
        }
        catch (Exception ex)
        {
            return new ScriptResponse
            {
                Key = "output-error",
                Data = new
                {
                    error = "Internal processing error",
                    errorCode = "PROCESSING_ERROR",
                    errorDescription = ex.Message
                },
                Tags = new[] { "appointments", "error" }
            };
        }
    }

    #region Helper Methods

    private string ClassifyServiceError(string errorMessage)
    {
        if (errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            return "timeout";
        if (errorMessage.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
            return "authentication";
        if (errorMessage.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
            return "authorization";
        if (errorMessage.Contains("not found", StringComparison.OrdinalIgnoreCase))
            return "not-found";
        if (errorMessage.Contains("service unavailable", StringComparison.OrdinalIgnoreCase))
            return "service-unavailable";
        if (errorMessage.Contains("connection", StringComparison.OrdinalIgnoreCase))
            return "connection-error";

        return "general-error";
    }

    private bool IsRetryableServiceError(string errorMessage)
    {
        return errorMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("service unavailable", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("connection", StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Business Logic Methods

    private dynamic FindPublicHoliday(List<dynamic> holidays, DateTime date)
    {
        foreach (var holiday in holidays)
        {
            // Instance structure: { attributes: {...} }
            var holidayData = holiday?.attributes;
            if (holidayData == null) continue;

            var entryMode = holidayData?.entryMode?.ToString();

            if (entryMode == "full-day")
            {
                var startDateStr = holidayData?.startDate?.ToString();
                var endDateStr = holidayData?.endDate?.ToString();
                
                if (!string.IsNullOrEmpty(startDateStr) && !string.IsNullOrEmpty(endDateStr))
                {
                    var startDate = DateTime.Parse(startDateStr);
                    var endDate = DateTime.Parse(endDateStr);
                    if (date.Date >= startDate.Date && date.Date <= endDate.Date)
                        return holidayData;
                }
            }
            else if (entryMode == "time-range")
            {
                var startDateTimeStr = holidayData?.startDateTime?.ToString();
                var endDateTimeStr = holidayData?.endDateTime?.ToString();
                
                if (!string.IsNullOrEmpty(startDateTimeStr) && !string.IsNullOrEmpty(endDateTimeStr))
                {
                    var startDateTime = DateTime.Parse(startDateTimeStr);
                    var endDateTime = DateTime.Parse(endDateTimeStr);
                    if (date.Date >= startDateTime.Date && date.Date <= endDateTime.Date)
                        return holidayData;
                }
            }
        }
        return null;
    }

    private List<object> GetWorkingHoursForDay(List<dynamic> workingHoursChanges, string dayOfWeek, DateTime date, ScriptContext context)
    {
        // Check for custom working hours
        foreach (var change in workingHoursChanges)
        {
            // Instance structure: { attributes: {...} }
            var changeData = change?.attributes;
            if (changeData == null) continue;

            var customWH = changeData?.customWorkingHours;
            if (customWH == null) continue;

            var effectiveFromStr = customWH?.effectiveFrom?.ToString();
            var effectiveToStr = customWH?.effectiveTo?.ToString();
            
            if (string.IsNullOrEmpty(effectiveFromStr)) continue;
            
            var effectiveFrom = DateTime.Parse(effectiveFromStr);
            var effectiveTo = string.IsNullOrEmpty(effectiveToStr) ? DateTime.MaxValue : DateTime.Parse(effectiveToStr);

            if (date.Date >= effectiveFrom.Date && date.Date <= effectiveTo.Date)
            {
                var dayHours = GetDayHoursFromCustom(customWH, dayOfWeek);
                if (dayHours != null) return dayHours;
            }
        }

        // Return default working hours from config
        return GetDefaultWorkingHours(dayOfWeek, context);
    }

    private List<object> GetDayHoursFromCustom(dynamic customWH, string dayOfWeek)
    {
        dynamic dayHours = null;
        switch (dayOfWeek)
        {
            case "monday": dayHours = customWH?.monday; break;
            case "tuesday": dayHours = customWH?.tuesday; break;
            case "wednesday": dayHours = customWH?.wednesday; break;
            case "thursday": dayHours = customWH?.thursday; break;
            case "friday": dayHours = customWH?.friday; break;
            case "saturday": dayHours = customWH?.saturday; break;
            case "sunday": dayHours = customWH?.sunday; break;
        }

        if (dayHours == null) return null;

        var result = new List<object>();
        foreach (var range in dayHours)
        {
            var start = range?.start?.ToString();
            var end = range?.end?.ToString();
            if (!string.IsNullOrEmpty(start) && !string.IsNullOrEmpty(end))
            {
                result.Add(new { start, end });
            }
        }
        return result.Count > 0 ? result : null;
    }

    private List<object> GetDefaultWorkingHours(string dayOfWeek, ScriptContext context)
    {
        // Get from appsettings.json configuration using GetConfigValue
        // Config key format: "WorkingHours:Monday" = "09:00-12:00,13:30-18:00"
        var dayKey = char.ToUpper(dayOfWeek[0]) + dayOfWeek.Substring(1); // monday -> Monday
        var configKey = $"WorkingHours:{dayKey}";
        var configValue = GetConfigValue(configKey);

        if (!string.IsNullOrEmpty(configValue))
        {
            var configHours = ParseWorkingHoursConfig(configValue);
            if (configHours != null && configHours.Count > 0) return configHours;
        }

        // Default working hours if not configured
        if (dayOfWeek == "saturday" || dayOfWeek == "sunday")
            return new List<object>();

        return new List<object>
        {
            new { start = "09:00", end = "12:00" },
            new { start = "13:30", end = "18:00" }
        };
    }

    private List<object> ParseWorkingHoursConfig(string configValue)
    {
        // Parse config value format: "09:00-12:00,13:30-18:00"
        var result = new List<object>();
        
        if (string.IsNullOrEmpty(configValue))
            return result;

        var ranges = configValue.Split(',');
        foreach (var range in ranges)
        {
            var trimmed = range.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var parts = trimmed.Split('-');
            if (parts.Length == 2)
            {
                var start = parts[0].Trim();
                var end = parts[1].Trim();
                if (!string.IsNullOrEmpty(start) && !string.IsNullOrEmpty(end))
                {
                    result.Add(new { start, end });
                }
            }
        }
        
        return result;
    }

    private List<(TimeSpan Start, TimeSpan End)> GetUnavailableRanges(List<dynamic> personalLeaves, DateTime date)
    {
        var ranges = new List<(TimeSpan Start, TimeSpan End)>();

        foreach (var leave in personalLeaves)
        {
            // Instance structure: { attributes: {...} }
            var leaveData = leave?.attributes;
            if (leaveData == null) continue;

            var entryMode = leaveData?.entryMode?.ToString();

            if (entryMode == "full-day")
            {
                var startDateStr = leaveData?.startDate?.ToString();
                var endDateStr = leaveData?.endDate?.ToString();
                
                if (!string.IsNullOrEmpty(startDateStr) && !string.IsNullOrEmpty(endDateStr))
                {
                    var startDate = DateTime.Parse(startDateStr);
                    var endDate = DateTime.Parse(endDateStr);
                    if (date.Date >= startDate.Date && date.Date <= endDate.Date)
                    {
                        ranges.Add((TimeSpan.Zero, new TimeSpan(23, 59, 59)));
                    }
                }
            }
            else if (entryMode == "time-range")
            {
                var startDateTimeStr = leaveData?.startDateTime?.ToString();
                var endDateTimeStr = leaveData?.endDateTime?.ToString();
                
                if (!string.IsNullOrEmpty(startDateTimeStr) && !string.IsNullOrEmpty(endDateTimeStr))
                {
                    var startDateTime = DateTime.Parse(startDateTimeStr);
                    var endDateTime = DateTime.Parse(endDateTimeStr);

                    if (startDateTime.Date == date.Date)
                    {
                        var startTime = startDateTime.TimeOfDay;
                        var endTime = endDateTime.Date == date.Date ? endDateTime.TimeOfDay : new TimeSpan(23, 59, 59);
                        ranges.Add((startTime, endTime));
                    }
                }
            }
        }

        return ranges;
    }

    private List<string> CalculateAvailableSlots(List<object> workingHours, List<(TimeSpan Start, TimeSpan End)> unavailableRanges, int durationMinutes)
    {
        var slots = new List<string>();
        var duration = TimeSpan.FromMinutes(durationMinutes);

        foreach (var wh in workingHours)
        {
            dynamic whDyn = wh;
            var startStr = whDyn?.start?.ToString();
            var endStr = whDyn?.end?.ToString();

            if (string.IsNullOrEmpty(startStr) || string.IsNullOrEmpty(endStr))
                continue;

            var whStart = TimeSpan.Parse(startStr);
            var whEnd = TimeSpan.Parse(endStr);

            // Skip if start == end (closed)
            if (whStart == whEnd)
                continue;

            var current = whStart;
            while (current + duration <= whEnd)
            {
                var slotStart = current;
                var slotEnd = current + duration;

                // Check if slot overlaps with any unavailable range
                bool isAvailable = true;
                foreach (var ur in unavailableRanges)
                {
                    if (slotStart < ur.End && slotEnd > ur.Start)
                    {
                        isAvailable = false;
                        break;
                    }
                }

                if (isAvailable)
                {
                    slots.Add($"{slotStart:hh\\:mm}-{slotEnd:hh\\:mm}");
                }

                current = current + duration;
            }
        }

        return slots;
    }

    #endregion
}
