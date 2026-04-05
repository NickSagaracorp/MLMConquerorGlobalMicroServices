using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MLMConquerorGlobalEdition.Domain.Entities.Support;
using MLMConquerorGlobalEdition.Repository.Context;

namespace MLMConquerorGlobalEdition.TicketManagementSystem.Services;

public class RoutingEngine : IRoutingEngine
{
    private readonly AppDbContext _db;

    public RoutingEngine(AppDbContext db) => _db = db;

    public async Task<RoutingResult> RouteAsync(Ticket ticket, CancellationToken ct = default)
    {
        // Find teams that handle this category
        var teams = await _db.SupportTeams
            .Include(t => t.Agents)
            .Where(t => t.IsActive)
            .ToListAsync(ct);

        var categories = await _db.TicketCategories
            .Where(c => c.Id == ticket.CategoryId)
            .ToListAsync(ct);

        var categoryDefaultTeamId = categories.FirstOrDefault()?.DefaultTeamId;

        SupportTeam? selectedTeam = null;

        if (categoryDefaultTeamId.HasValue)
            selectedTeam = teams.FirstOrDefault(t => t.Id == categoryDefaultTeamId.Value);

        selectedTeam ??= teams.FirstOrDefault();

        if (selectedTeam is null)
            return new RoutingResult(null, null, false, null);

        // Build candidate pool
        var candidates = selectedTeam.Agents
            .Where(a => a.IsActive && a.Availability != "offline")
            .ToList();

        // Rule 2: Language filter
        if (!string.IsNullOrWhiteSpace(ticket.Language) && ticket.Language != "es")
        {
            var langFiltered = candidates.Where(a =>
            {
                var langs = JsonSerializer.Deserialize<List<string>>(a.LanguagesJson) ?? new List<string>();
                return langs.Contains(ticket.Language, StringComparer.OrdinalIgnoreCase);
            }).ToList();
            if (langFiltered.Count > 0) candidates = langFiltered;
        }

        // Rule 3: Tier filter for Critical priority
        if (ticket.Priority == TicketPriority.Critical)
        {
            var tierFiltered = candidates.Where(a => a.Tier >= 2).ToList();
            if (tierFiltered.Count > 0) candidates = tierFiltered;
        }

        // Rule 4: Premium customer tier
        if (ticket.CustomerTier?.ToLowerInvariant() == "premium")
        {
            var tierFiltered = candidates.Where(a => a.Tier >= 2).ToList();
            if (tierFiltered.Count > 0) candidates = tierFiltered;
        }

        // Rule 6: Capacity check
        candidates = candidates
            .Where(a => a.CurrentTicketCount < a.MaxConcurrentTickets)
            .ToList();

        if (candidates.Count == 0)
        {
            // Rule 7: Fallback to supervisor
            return new RoutingResult(null, selectedTeam.Id, true, selectedTeam.SupervisorAgentId);
        }

        // Rule 5: Distribution method
        SupportAgent? selected = selectedTeam.RoutingMethod switch
        {
            "least_busy" => candidates.OrderBy(a => a.CurrentTicketCount).First(),
            "manual"     => null,
            _            => candidates[0]   // round_robin — first available (ordering by count ascending gives balance)
        };

        return new RoutingResult(selected?.Id, selectedTeam.Id, false, null);
    }
}
