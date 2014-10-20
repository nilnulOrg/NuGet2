﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JsonLD.Core;
using Newtonsoft.Json.Linq;
using NuGet.Client.Diagnostics;
using NuGet.Client.Installation;
using NuGet.Client.Resolution;

namespace NuGet.Client.Interop
{
    public class V2SourceRepository : SourceRepository
    {
        private readonly IPackageRepository _repository;
        private readonly LocalPackageRepository _lprepo;
        private readonly PackageSource _source;
        private readonly string _userAgent;

        public override PackageSource Source { get { return _source; } }

        public V2SourceRepository(PackageSource source, IPackageRepository repository, string host)
        {
            _source = source;
            _repository = repository;
            
            // TODO: Get context from current UI activity (PowerShell, Dialog, etc.)
            _userAgent = UserAgentUtil.GetUserAgent("NuGet.Client.Interop", host);

            var events = _repository as IHttpClientEvents;
            if (events != null)
            {
                events.SendingRequest += (sender, args) =>
                {
                    NuGetTraceSources.V2SourceRepository.Verbose("http", "{0} {1}", args.Request.Method, args.Request.RequestUri.ToString());
                };
            }

            _lprepo = _repository as LocalPackageRepository;
        }

        public override Task<IEnumerable<JObject>> Search(string searchTerm, SearchFilter filters, int skip, int take, CancellationToken cancellationToken)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("search", "Searching for '{0}'", searchTerm);
            return Task.Factory.StartNew(() => {
                return (IEnumerable<JObject>)_repository.Search(
                    searchTerm,
                    filters.SupportedFrameworks.Select(fx => fx.FullName),
                    filters.IncludePrerelease)
                    .Skip(skip)
                    .Take(take)
                    .ToList()
                    .AsParallel()
                    .AsOrdered()
                    .Select(p => CreatePackageSearchResult(p))
                    .ToList();
             } , cancellationToken);
        }

        private JObject CreatePackageSearchResult(IPackage package)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("getallvers", "Retrieving all versions for {0}", package.Id);
            var versions = _repository.FindPackagesById(package.Id);
            if (!versions.Any())
            {
                versions = new[] { package };
            }

            string repoRoot = null;
            IPackagePathResolver resolver = null;
            if (_lprepo != null)
            {
                repoRoot = _lprepo.Source;
                resolver = _lprepo.PathResolver;
            }

            return PackageJsonLd.CreatePackageSearchResult(package, versions, repoRoot, resolver);
        }

        public override Task<JObject> GetPackageMetadata(string id, Versioning.NuGetVersion version)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("getpackage", "Getting metadata for {0} {1}", id, version);
            var package = _repository.FindPackage(id, CoreConverters.SafeToSemVer(version));
            if (package == null)
            {
                return Task.FromResult<JObject>(null);
            }

            string repoRoot = null;
            IPackagePathResolver resolver = null;
            if (_lprepo != null)
            {
                repoRoot = _lprepo.Source;
                resolver = _lprepo.PathResolver;
            }

            return Task.FromResult(PackageJsonLd.CreatePackage(package, repoRoot, resolver));
        }

        public override Task<IEnumerable<JObject>> GetPackageMetadataById(string packageId)
        {
            NuGetTraceSources.V2SourceRepository.Verbose("findpackagebyid", "Getting metadata for all versions of {0}", packageId);
            string repoRoot = null;
            IPackagePathResolver resolver = null;
            if (_lprepo != null)
            {
                repoRoot = _lprepo.Source;
                resolver = _lprepo.PathResolver;
            }
            return Task.FromResult(_repository.FindPackagesById(packageId).Select(p => PackageJsonLd.CreatePackage(p, repoRoot, resolver)));
        }

        public override void RecordMetric(PackageActionType actionType, PackageIdentity packageIdentity, PackageIdentity dependentPackage, bool isUpdate, InstallationTarget target)
        {
            // No-op, V2 doesn't support this.
        }
    }
}
