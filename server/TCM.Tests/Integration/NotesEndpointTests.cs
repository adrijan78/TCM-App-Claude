using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TCM.Application.Common;
using TCM.Application.Dtos.Account;
using TCM.Application.Dtos.Notes;
using TCM.Domain.Entities;
using TCM.Domain.Enums;
using TCM.Infrastructure.Persistence;

namespace TCM.Tests.Integration;

/// <summary>
/// The notes slice of SPEC sections 6.4, 6.6 and 6.8, read through the role matrix of section 5.
/// The two rules worth the most here: a member must never reach another member's notes by
/// changing an id in the URL, and "delete only own notes" means the note they <em>wrote</em>.
/// </summary>
public class NotesEndpointTests(TcmApiFactory factory) : IClassFixture<TcmApiFactory>
{
    private async Task<HttpClient> ClientAsAsync(string email)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/account/login", new LoginMemberDto(email, TcmApiFactory.Password));
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<ApiResponse<MemberTokenDto>>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body!.Data!.Token);
        return client;
    }

    private static async Task<NoteDto> CreateNoteAsync(
        HttpClient client, string toMemberId, string title, NotePriority priority, int? trainingId = null)
    {
        var response = await client.PostAsJsonAsync("/api/notes",
            new CreateNoteDto(title, "Body of " + title, priority, toMemberId, trainingId));

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NoteDto>>();
        return body!.Data!;
    }

    private static async Task<List<NoteDto>> ReadNotesAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<NoteDto>>>();
        Assert.True(body!.Success);
        return body.Data!;
    }

    /// <summary>
    /// Inserted straight into the database: the trainings slice is another agent's, and the notes
    /// panel of section 6.6 only needs a training row to exist to be exercised.
    /// </summary>
    private async Task<int> CreateTrainingAsync(string description)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var clubId = await db.Users
            .Where(u => u.Id == factory.CoachId)
            .Select(u => u.ClubId!.Value)
            .FirstAsync();

        var training = new Training
        {
            Date = DateTime.UtcNow,
            Description = description,
            MemberId = factory.CoachId,
            ClubId = clubId
        };

        db.Trainings.Add(training);
        await db.SaveChangesAsync();

        return training.Id;
    }

    // ---- GET /api/notes — club-wide, coach only (SPEC section 6.8) -----------------------------

    [Fact]
    public async Task GetClubNotes_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetClubNotes_AsMember_Returns403()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.GetAsync("/api/notes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetClubNotes_AsCoach_ReturnsNotesAboutEveryMember()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await CreateNoteAsync(coach, factory.MemberId, "CLUBWIDE first member", NotePriority.Medium);
        await CreateNoteAsync(coach, factory.OtherMemberId, "CLUBWIDE second member", NotePriority.Low);

        var response = await coach.GetAsync("/api/notes?search=CLUBWIDE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await ReadNotesAsync(response);
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, n => n.ToMemberId == factory.MemberId);
        Assert.Contains(notes, n => n.ToMemberId == factory.OtherMemberId);
    }

    [Fact]
    public async Task GetClubNotes_WithSearch_FiltersByTitle()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await CreateNoteAsync(coach, factory.MemberId, "SEARCHABLE sparring form", NotePriority.Low);
        await CreateNoteAsync(coach, factory.MemberId, "Something else entirely", NotePriority.Low);

        var response = await coach.GetAsync("/api/notes?search=SEARCHABLE");

        var notes = await ReadNotesAsync(response);
        Assert.All(notes, n => Assert.Contains("SEARCHABLE", n.Title));
        Assert.Single(notes);
    }

    // ---- GET /api/notes/member/{id} — the profile panel (SPEC section 6.4) ---------------------

    [Fact]
    public async Task GetMemberNotes_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/notes/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMemberNotes_OfAnotherMember_AsMember_Returns403()
    {
        // The single most likely security bug in this app: an id swapped in the URL.
        var client = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);

        var response = await client.GetAsync($"/api/notes/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<NoteDto>>>();
        Assert.False(body!.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task GetMemberNotes_OfSelf_AsMember_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        await CreateNoteAsync(coach, factory.MemberId, "OWNPROFILE keep the guard up", NotePriority.High);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.GetAsync($"/api/notes/member/{factory.MemberId}?search=OWNPROFILE");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await ReadNotesAsync(response);
        Assert.Single(notes);
        Assert.Equal(factory.CoachId, notes[0].FromMemberId);
    }

    [Fact]
    public async Task GetMemberNotes_OfAnyMemberInOwnClub_AsCoach_Returns200()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.GetAsync($"/api/notes/member/{factory.OtherMemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetMemberNotes_OrdersHighPriorityFirstThenNewest()
    {
        // SPEC section 6.8 fixes this order, and the ordering is done in SQL.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await CreateNoteAsync(coach, factory.MemberId, "ORDERED low", NotePriority.Low);
        await CreateNoteAsync(coach, factory.MemberId, "ORDERED high older", NotePriority.High);
        await CreateNoteAsync(coach, factory.MemberId, "ORDERED medium", NotePriority.Medium);
        await CreateNoteAsync(coach, factory.MemberId, "ORDERED high newer", NotePriority.High);

        var response = await coach.GetAsync($"/api/notes/member/{factory.MemberId}?search=ORDERED");

        var notes = await ReadNotesAsync(response);
        Assert.Equal(4, notes.Count);
        Assert.Equal(
            ["ORDERED high newer", "ORDERED high older", "ORDERED medium", "ORDERED low"],
            notes.Select(n => n.Title).ToArray());
    }

    // ---- GET /api/notes/training/{trainingId}/member/{memberId} (SPEC section 6.6) -------------

    [Fact]
    public async Task GetTrainingNotes_Anonymously_Returns401()
    {
        var trainingId = await CreateTrainingAsync("Anonymous probe");
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/notes/training/{trainingId}/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetTrainingNotes_OfAnotherMember_AsMember_Returns403()
    {
        var trainingId = await CreateTrainingAsync("Cross-member probe");
        var client = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);

        var response = await client.GetAsync($"/api/notes/training/{trainingId}/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetTrainingNotes_OfSelf_AsMember_ReturnsOnlyThatTrainingsNotes()
    {
        var trainingId = await CreateTrainingAsync("Sparring session");
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        await CreateNoteAsync(coach, factory.MemberId, "PANEL attached to training", NotePriority.Medium, trainingId);
        await CreateNoteAsync(coach, factory.MemberId, "PANEL not attached", NotePriority.High);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.GetAsync($"/api/notes/training/{trainingId}/member/{factory.MemberId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notes = await ReadNotesAsync(response);
        Assert.Single(notes);
        Assert.Equal("PANEL attached to training", notes[0].Title);
        Assert.Equal(trainingId, notes[0].TrainingId);
        Assert.Equal("Sparring session", notes[0].TrainingDescription);
    }

    // ---- POST /api/notes (SPEC sections 5 and 6.8) ----------------------------------------------

    [Fact]
    public async Task CreateNote_Anonymously_Returns401()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/notes",
            new CreateNoteDto("Anonymous", "Body", NotePriority.Low, factory.MemberId, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_AboutAnotherMember_AsMember_Returns403()
    {
        // "Notes about another member" is coach-only in SPEC section 5.
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsJsonAsync("/api/notes",
            new CreateNoteDto("Sneaky", "Not allowed", NotePriority.High, factory.OtherMemberId, null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateNote_AboutSelf_AsMember_RecordsTheCallerAsAuthor()
    {
        var client = await ClientAsAsync(TcmApiFactory.MemberEmail);

        var response = await client.PostAsJsonAsync("/api/notes",
            new CreateNoteDto("SELFNOTE stretch more", "Body", NotePriority.Medium, factory.MemberId, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NoteDto>>();
        Assert.True(body!.Success);
        Assert.Equal(factory.MemberId, body.Data!.FromMemberId);
        Assert.Equal(factory.MemberId, body.Data.ToMemberId);
    }

    [Fact]
    public async Task CreateNote_AsCoach_IsAuthoredByTheCoachNotTheSubject()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var created = await CreateNoteAsync(coach, factory.MemberId, "AUTHORSHIP check", NotePriority.Low);

        Assert.Equal(factory.CoachId, created.FromMemberId);
        Assert.Equal(factory.MemberId, created.ToMemberId);
    }

    [Fact]
    public async Task CreateNote_WithBlankTitle_IsRejected()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync("/api/notes",
            new CreateNoteDto("   ", "Body", NotePriority.Low, factory.MemberId, null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<NoteDto>>();
        Assert.False(body!.Success);
        Assert.NotEmpty(body.Errors);
    }

    [Fact]
    public async Task CreateNote_ForAnUnknownMember_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.PostAsJsonAsync("/api/notes",
            new CreateNoteDto("Ghost", "Body", NotePriority.Low, "no-such-member-id", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ---- DELETE /api/notes/{id} (SPEC section 5) ------------------------------------------------

    [Fact]
    public async Task DeleteNote_Anonymously_Returns401()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var note = await CreateNoteAsync(coach, factory.MemberId, "DELETE anonymous probe", NotePriority.Low);

        var anonymous = factory.CreateClient();
        var response = await anonymous.DeleteAsync($"/api/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_TheMemberDidNotAuthor_AsMember_Returns403()
    {
        // Being the subject of a note is not authorship: the coach's note stays.
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var note = await CreateNoteAsync(coach, factory.MemberId, "DELETEKEEP coach wrote this", NotePriority.High);

        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await member.DeleteAsync($"/api/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var stillThere = await member.GetAsync($"/api/notes/member/{factory.MemberId}?search=DELETEKEEP");
        Assert.Single(await ReadNotesAsync(stillThere));
    }

    [Fact]
    public async Task DeleteNote_TheMemberAuthored_AsMember_Returns200()
    {
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var note = await CreateNoteAsync(member, factory.MemberId, "DELETEOWN member wrote this", NotePriority.Low);

        var response = await member.DeleteAsync($"/api/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var remaining = await member.GetAsync($"/api/notes/member/{factory.MemberId}?search=DELETEOWN");
        Assert.Empty(await ReadNotesAsync(remaining));
    }

    [Fact]
    public async Task DeleteNote_AnotherMembersOwnNote_AsMember_Returns403()
    {
        var author = await ClientAsAsync(TcmApiFactory.OtherMemberEmail);
        var note = await CreateNoteAsync(author, factory.OtherMemberId, "DELETE someone elses", NotePriority.Low);

        var stranger = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var response = await stranger.DeleteAsync($"/api/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_AnyNoteInOwnClub_AsCoach_Returns200()
    {
        var member = await ClientAsAsync(TcmApiFactory.MemberEmail);
        var note = await CreateNoteAsync(member, factory.MemberId, "DELETE coach may remove", NotePriority.Medium);

        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);
        var response = await coach.DeleteAsync($"/api/notes/{note.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task DeleteNote_ThatDoesNotExist_Returns404()
    {
        var coach = await ClientAsAsync(TcmApiFactory.CoachEmail);

        var response = await coach.DeleteAsync("/api/notes/987654");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
