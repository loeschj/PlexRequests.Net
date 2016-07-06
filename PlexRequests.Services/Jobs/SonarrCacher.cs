﻿#region Copyright
// /************************************************************************
//    Copyright (c) 2016 Jamie Rees
//    File: PlexAvailabilityChecker.cs
//    Created By: Jamie Rees
//   
//    Permission is hereby granted, free of charge, to any person obtaining
//    a copy of this software and associated documentation files (the
//    "Software"), to deal in the Software without restriction, including
//    without limitation the rights to use, copy, modify, merge, publish,
//    distribute, sublicense, and/or sell copies of the Software, and to
//    permit persons to whom the Software is furnished to do so, subject to
//    the following conditions:
//   
//    The above copyright notice and this permission notice shall be
//    included in all copies or substantial portions of the Software.
//   
//    THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
//    EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
//    MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
//    NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
//    LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
//    OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
//    WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
//  ************************************************************************/
#endregion
using System;
using System.Collections.Generic;
using System.Linq;

using NLog;

using PlexRequests.Api.Interfaces;
using PlexRequests.Api.Models.Sonarr;
using PlexRequests.Core;
using PlexRequests.Core.SettingModels;
using PlexRequests.Helpers;
using PlexRequests.Services.Interfaces;
using PlexRequests.Store.Models;
using PlexRequests.Store.Repository;

using Quartz;

namespace PlexRequests.Services.Jobs
{
    public class SonarrCacher : IJob, ISonarrCacher
    {
        public SonarrCacher(ISettingsService<SonarrSettings> sonarrSettings, ISonarrApi sonarrApi, ICacheProvider cache, IJobRecord rec)
        {
            SonarrSettings = sonarrSettings;
            SonarrApi = sonarrApi;
            Job = rec;
            Cache = cache;
        }

        private ISettingsService<SonarrSettings> SonarrSettings { get; }
        private ICacheProvider Cache { get; }
        private ISonarrApi SonarrApi { get; }
        private IJobRecord Job { get; }

        private static Logger Log = LogManager.GetCurrentClassLogger();

        public void Queued()
        {
            Log.Trace("Getting the settings");

            var settings = SonarrSettings.GetSettings();
            if (settings.Enabled)
            {
                Log.Trace("Getting all tv series from Sonarr");
                try
                {
                    var series = SonarrApi.GetSeries(settings.ApiKey, settings.FullUri);
                    if (series != null)
                    {
                        Cache.Set(CacheKeys.SonarrQueued, series, CacheKeys.TimeFrameMinutes.SchedulerCaching);
                    }
                }
                catch (System.Exception ex)
                {
                    Log.Error(ex, "Failed caching queued items from Sonarr");
                }
                finally
                {
                    Job.Record(JobNames.SonarrCacher);
                }
            }
        }

        // we do not want to set here...
        public int[] QueuedIds()
        {
            var series = Cache.Get<List<Series>>(CacheKeys.SonarrQueued);
            return series?.Select(x => x.tvdbId).ToArray() ?? new int[] { };
        }

        public void Execute(IJobExecutionContext context)
        {
            try
            {
                Queued();
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }
    }
}