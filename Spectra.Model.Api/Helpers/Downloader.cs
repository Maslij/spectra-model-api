﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Spectra.Model.Api.Helpers
{
    class Downloader
    {
        private volatile bool _completed;

        public void DownloadFile(string address, string location)
        {
            using (WebClient client = new WebClient())
            {
                Uri Uri = new Uri(address);
                _completed = false;

                client.DownloadFileCompleted += new AsyncCompletedEventHandler(Completed);

                client.DownloadProgressChanged += new DownloadProgressChangedEventHandler(DownloadProgress);
                client.DownloadFileAsync(Uri, location);
            }
        }

        public bool DownloadCompleted { get { return _completed; } }

        private void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            // Displays the operation identifier, and the transfer progress.
            Console.WriteLine("{0}    downloaded {1} of {2} bytes. {3} % complete...",
                (string)e.UserState,
                e.BytesReceived,
                e.TotalBytesToReceive,
                e.ProgressPercentage);
        }

        private void Completed(object sender, AsyncCompletedEventArgs e)
        {
            if (e.Cancelled == true)
            {
                Console.WriteLine("Download has been canceled.");
            }
            else
            {
                Console.WriteLine("Download completed!");
            }

            _completed = true;
        }
    }
}
