Hier ist ein **praxisnahes C#-Beispiel** für EasyRedmine / Redmine, um **Zeiteinträge zu erstellen**, abzurufen und zu ändern – sauber mit `HttpClient`.

***

# ✅ 1. Setup (HttpClient)

```csharp
using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

class RedmineClient
{
    private readonly HttpClient _client;

    public RedmineClient(string baseUrl, string apiKey)
    {
        _client = new HttpClient
        {
            BaseAddress = new Uri(baseUrl)
        };

        _client.DefaultRequestHeaders.Add("X-Redmine-API-Key", apiKey);
    }

    public HttpClient Client => _client;
}
```

***

# ✅ 2. Zeiteintrag erstellen

```csharp
public async Task CreateTimeEntry()
{
    var redmine = new RedmineClient("https://your-redmine.com/", "YOUR_API_KEY");

    var payload = new
    {
        time_entry = new
        {
            issue_id = 123,
            hours = 2.5,
            spent_on = DateTime.Today.ToString("yyyy-MM-dd"),
            activity_id = 9,
            comments = "API Zeiteintrag aus C#"
        }
    };

    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await redmine.Client.PostAsync("time_entries.json", content);

    if (response.IsSuccessStatusCode)
    {
        Console.WriteLine("Zeiteintrag erstellt!");
    }
    else
    {
        var error = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Fehler: {response.StatusCode} - {error}");
    }
}
```

***

# ✅ 3. Zeiteinträge abrufen

```csharp
public async Task GetTimeEntries()
{
    var redmine = new RedmineClient("https://your-redmine.com/", "YOUR_API_KEY");

    var response = await redmine.Client.GetAsync("time_entries.json?user_id=me");

    var content = await response.Content.ReadAsStringAsync();

    Console.WriteLine(content);
}
```

***

# ✅ 4. Zeiteintrag ändern

```csharp
public async Task UpdateTimeEntry(int id)
{
    var redmine = new RedmineClient("https://your-redmine.com/", "YOUR_API_KEY");

    var payload = new
    {
        time_entry = new
        {
            hours = 3.0,
            comments = "Zeit korrigiert"
        }
    };

    var json = JsonSerializer.Serialize(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await redmine.Client.PutAsync($"time_entries/{id}.json", content);

    Console.WriteLine(response.IsSuccessStatusCode
        ? "Update erfolgreich"
        : "Update fehlgeschlagen");
}
```

***

# ✅ 5. Activity IDs holen

```csharp
public async Task GetActivities()
{
    var redmine = new RedmineClient("https://your-redmine.com/", "YOUR_API_KEY");

    var response = await redmine.Client.GetAsync("enumerations/time_entry_activities.json");
    var content = await response.Content.ReadAsStringAsync();

    Console.WriteLine(content);
}
```

***

# ✅ 6. Mit Custom Fields (EasyRedmine wichtig)

```csharp
var payload = new
{
    time_entry = new
    {
        issue_id = 123,
        hours = 2,
        spent_on = "2026-06-05",
        activity_id = 9,
        custom_fields = new[]
        {
            new { id = 1, value = "extern" }
        }
    }
};
```

***

# 💡 Tipps aus der Praxis

* ✅ `hours` als `double` (z. B. 1.25)
* ✅ Datum immer `yyyy-MM-dd`
* ✅ `activity_id` muss gültig sein (sonst 422 Fehler)
* ✅ Fehler (422) kommen oft von:
  * Pflichtfeldern
  * falscher Activity
  * gesperrten Zeiträumen

***

# 🔥 Bonus: saubere DTO-Klassen (optional)

```csharp
public class TimeEntryRequest
{
    public TimeEntry time_entry { get; set; }
}

public class TimeEntry
{
    public int issue_id { get; set; }
    public double hours { get; set; }
    public string spent_on { get; set; }
    public int activity_id { get; set; }
    public string comments { get; set; }
}
```

***

# ✅ Fazit

Mit C# ist die EasyRedmine API:

* extrem einfach über `HttpClient`
* vollständig JSON-basiert
* schnell integrierbar in Tools, Services oder Add-ins

***

✅ Wenn du willst, kann ich dir als nächsten Schritt bauen:

* ein **fertiges .NET Service/Wrapper**
* oder eine **komplette Integration (z. B. mit Login, Userauswahl, Caching)**
