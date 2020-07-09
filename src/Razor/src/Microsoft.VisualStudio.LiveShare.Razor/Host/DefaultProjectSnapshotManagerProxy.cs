// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LiveShare.Razor.Host
{
    internal class DefaultProjectSnapshotManagerProxy : IProjectSnapshotManagerProxy, ICollaborationService, IDisposable
    {
        private readonly CollaborationSession _session;
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly ProjectSnapshotManager _projectSnapshotManager;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly AsyncSemaphore _latestStateSemaphore;
        private bool _disposed;
        private ProjectSnapshotManagerProxyState _latestState;

        // Internal for testing
        internal JoinableTask _processingChangedEventTestTask;

        public DefaultProjectSnapshotManagerProxy(
            CollaborationSession session,
            ForegroundDispatcher foregroundDispatcher,
            ProjectSnapshotManager projectSnapshotManager,
            JoinableTaskFactory joinableTaskFactory)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (projectSnapshotManager == null)
            {
                throw new ArgumentNullException(nameof(projectSnapshotManager));
            }

            if (joinableTaskFactory == null)
            {
                throw new ArgumentNullException(nameof(joinableTaskFactory));
            }

            _session = session;
            _foregroundDispatcher = foregroundDispatcher;
            _projectSnapshotManager = projectSnapshotManager;
            _joinableTaskFactory = joinableTaskFactory;

            _latestStateSemaphore = new AsyncSemaphore(initialCount: 1);
            _projectSnapshotManager.Changed += ProjectSnapshotManager_Changed;
        }

        public event EventHandler<ProjectChangeEventProxyArgs> Changed;

        public async Task<ProjectSnapshotManagerProxyState> GetProjectManagerStateAsync(CancellationToken cancellationToken)
        {
            using (await _latestStateSemaphore.EnterAsync().ConfigureAwait(false))
            {
                if (_latestState != null)
                {
                    return _latestState;
                }
            }

            var projects = await GetLatestProjectsAsync();
            var state = await CalculateUpdatedStateAsync(projects);

            return state;
        }

        public void Dispose()
        {
            _foregroundDispatcher.AssertForegroundThread();

            _projectSnapshotManager.Changed -= ProjectSnapshotManager_Changed;
            _latestStateSemaphore.Dispose();
            _disposed = true;
        }

        // Internal for testing
        internal async Task<IReadOnlyList<ProjectSnapshot>> GetLatestProjectsAsync()
        {
            if (!_foregroundDispatcher.IsForegroundThread)
            {
                await _joinableTaskFactory.SwitchToMainThreadAsync(CancellationToken.None);
            }

            return _projectSnapshotManager.Projects.ToArray();
        }

        // Internal for testing
        internal async Task<ProjectSnapshotManagerProxyState> CalculateUpdatedStateAsync(IReadOnlyList<ProjectSnapshot> projects)
        {
            using (await _latestStateSemaphore.EnterAsync().ConfigureAwait(false))
            {
                var projectHandles = new List<ProjectSnapshotHandleProxy>();
                foreach (var project in projects)
                {
                    var projectHandleProxy = ConvertToProxy(project);
                    projectHandles.Add(projectHandleProxy);
                }

                _latestState = new ProjectSnapshotManagerProxyState(projectHandles);
                return _latestState;
            }
        }

        private ProjectSnapshotHandleProxy ConvertToProxy(ProjectSnapshot project)
        {
            var projectWorkspaceState = new ProjectWorkspaceState(project.TagHelpers, project.CSharpLanguageVersion);
            var projectFilePath = _session.ConvertLocalPathToSharedUri(project.FilePath);
            var projectHandleProxy = new ProjectSnapshotHandleProxy(projectFilePath, project.Configuration, project.RootNamespace, projectWorkspaceState);
            return projectHandleProxy;
        }

        private void ProjectSnapshotManager_Changed(object sender, ProjectChangeEventArgs args)
        {
            _foregroundDispatcher.AssertForegroundThread();

            if (_disposed)
            {
                return;
            }
            
            if (args.Kind == ProjectChangeKind.DocumentAdded ||
                args.Kind == ProjectChangeKind.DocumentRemoved ||
                args.Kind == ProjectChangeKind.DocumentChanged)
            {
                // Razor LiveShare doesn't currently support document based notifications over the wire.
                return;
            }

            _processingChangedEventTestTask = _joinableTaskFactory.RunAsync(async () =>
            {
                var projects = await GetLatestProjectsAsync();

                await _joinableTaskFactory.SwitchToMainThreadAsync();

                var oldProjectProxy = ConvertToProxy(args.Older);
                var newProjectProxy = ConvertToProxy(args.Newer);
                var remoteProjectChangeArgs = new ProjectChangeEventProxyArgs(oldProjectProxy, newProjectProxy, (ProjectProxyChangeKind)args.Kind);

                OnChanged(remoteProjectChangeArgs);
            });
        }

        private void OnChanged(ProjectChangeEventProxyArgs args)
        {
            _foregroundDispatcher.AssertForegroundThread();

            if (_disposed)
            {
                return;
            }

            Changed?.Invoke(this, args);
        }
    }
}
