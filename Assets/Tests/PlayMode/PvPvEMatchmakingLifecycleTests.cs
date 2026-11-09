#if UNITY_EDITOR
using System;
using System.Collections;
using System.Threading.Tasks;
using DeadQuarantine.PvPvE.Networking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class PvPvEMatchmakingLifecycleTests
{
    sealed class Lease : IPvPvESession
    {
        public string Id=>"controlled-late-result";public bool IsHost=>true;public int ParticipantCount=>1;public Task CloseAdmissionAsync()=>Task.CompletedTask;public event Action Closed;
        public int Leaves;public Task LeaveAsync(){Leaves++;return Task.CompletedTask;}
    }
    sealed class Directory : IPvPvESessions
    {
        public readonly TaskCompletionSource<bool> Init=new();public readonly TaskCompletionSource<IPvPvESession> Reply=new();public int Searches;
        public Task InitializeAsync()=>Init.Task;
        public Task<IPvPvESession> FindOrCreateAsync(PvPvENetworkSettings settings){Searches++;return Reply.Task;}
    }
    PvPvEMatchmaking service;PvPvENetworkSettings config;Directory directory;
    [SetUp] public void Setup()
    {
        config=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PvPvENetworkSettings>("Assets/PvPvE/Settings/PvPvENetworkSettings.asset"));
        service=new GameObject("Lifecycle service").AddComponent<PvPvEMatchmaking>();directory=new Directory();service.ConfigureForTest(config,directory);
    }
    [UnityTearDown] public IEnumerator Cleanup(){var task=service.CancelAndLeaveAsync();yield return Until(task);UnityEngine.Object.Destroy(service.gameObject);UnityEngine.Object.Destroy(config);yield return null;}
    static IEnumerator Until(Task task){float end=Time.realtimeSinceStartup+10;while(!task.IsCompleted){Assert.That(Time.realtimeSinceStartup,Is.LessThan(end));yield return null;}if(task.IsFaulted)throw task.Exception;}
    [UnityTest] public IEnumerator CancellingBeforeInitializationNeverCreatesASession()
    {
        var first=service.SearchAsync();Assert.That(service.SearchAsync(),Is.SameAs(first));var leave=service.CancelAndLeaveAsync();directory.Init.SetResult(true);yield return Until(leave);
        Assert.That(directory.Searches,Is.Zero);Assert.That(service.CanNavigateAway,Is.True);Assert.That(service.State,Is.EqualTo(PvPvEConnectionState.Idle));
    }
    [UnityTest] public IEnumerator LateResponseAfterCancelIsClosedBeforeAnotherSearch()
    {
        directory.Init.SetResult(true);var first=service.SearchAsync();Assert.That(directory.Searches,Is.EqualTo(1));
        var leave=service.CancelAndLeaveAsync();service.SearchAsync();Assert.That(directory.Searches,Is.EqualTo(1));Assert.That(service.CanNavigateAway,Is.False);
        var lease=new Lease();directory.Reply.SetResult(lease);yield return Until(leave);
        Assert.That(lease.Leaves,Is.EqualTo(1));Assert.That(service.CanNavigateAway,Is.True);Assert.That(service.Network,Is.Null);
    }
    [UnityTest] public IEnumerator ServiceFailureReleasesTheAttemptAndPermitsRetry()
    {
        directory.Init.SetException(new InvalidOperationException("Service unavailable"));yield return Until(service.SearchAsync());
        Assert.That(service.State,Is.EqualTo(PvPvEConnectionState.Failed));Assert.That(service.Error,Does.Contain("Service unavailable"));Assert.That(service.CanNavigateAway,Is.True);
    }
}
#endif
