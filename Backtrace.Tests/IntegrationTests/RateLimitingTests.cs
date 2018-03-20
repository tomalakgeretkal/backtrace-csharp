﻿using Backtrace.Base;
using Backtrace.Interfaces;
using Backtrace.Model;
using Backtrace.Services;
using Backtrace.Types;
using Moq;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace Backtrace.Tests.IntegrationTests
{
    /// <summary>
    /// Runs Integration Tests
    /// </summary>
    [TestFixture(Author = "Arthur Tu", Category = "Submission tests", Description = "Test rate limiting with diffrent threads")]
    public class RateLimitingTests
    {
        private BacktraceClient _backtraceClient;
        private bool reportLimitReached = false;

        /// <summary>
        /// Prepare basic setup of Backtrace client
        /// </summary>
        [SetUp]
        public void Setup()
        {
            //prepare mock object
            //mock api
            var api = new Mock<IBacktraceApi<object>>();
            api.Setup(n => n.Send(It.IsAny<BacktraceData<object>>()));

            //mock database
            var database = new Mock<IBacktraceDatabase<object>>();
            database.Setup(n => n.GenerateMiniDump(It.IsAny<BacktraceReportBase<object>>(), It.IsAny<MiniDumpType>()));

            var credentials = new BacktraceCredentials("https://validurl.com/", "validToken");
            _backtraceClient = new BacktraceClient(credentials)
            {
                _backtraceApi = api.Object,
                _database = database.Object
            };

            //Add new scoped attributes
            _backtraceClient.Attributes["ClientAttributeNumber"] = 1;
            _backtraceClient.Attributes["ClientAttributeString"] = "string attribute";
            _backtraceClient.Attributes["ClientAttributeCustomClass"] = new
            {
                Name = "BacktraceIntegrationTest",
                Type = "Library"
            };
            _backtraceClient.Attributes["ComplexObject"] = new Dictionary<string, Uri>()
            {
                {"backtrace.io" , new Uri("http://backtrace.io") },
                {"Google url" , new Uri("http://google.com") }
            };


            //to check if client report limit reached use OnClientReportLimitReached 
            _backtraceClient.OnClientReportLimitReached += () =>
            {
                reportLimitReached = true;
            };
        }

        private void DivideByZeroMethod()
        {

            int x = 0;
            var result = 5 / x;
        }

        private void OutOfRangeMethod()
        {
            int[] x = new int[1];
            x[1] = 1 - 1;
        }

        private void ThreadTest(int threadIndex, int totalSend = 0)
        {
            _backtraceClient.Send($"Custom client message");
            try
            {
                DivideByZeroMethod();
            }
            catch (DivideByZeroException divideException)
            {
                _backtraceClient.Send(divideException);
            }
            try
            {
                OutOfRangeMethod();
            }
            catch (IndexOutOfRangeException outOfRangeException)
            {
                _backtraceClient.Send(outOfRangeException);
            }
            _backtraceClient.Send($"End test case for thread {threadIndex}");
            totalSend += 4;
        }

        /// <summary>
        /// Test a initialization and submission sequence for backtrace client w/ threading w/o rate limiting
        /// </summary>
        [TestCase(1)]
        [TestCase(5)]
        [TestCase(10)]
        [Test(Author = "Arthur Tu and Konrad Dysput", Description = "Test rate limiting on single/multiple thread thread")]
        public void SingleThreadWithoutRateLimiting(int numberOfThreads)
        {
            reportLimitReached = false;
            _backtraceClient.ChangeRateLimiting(0);
            List<Thread> threads = new List<Thread>();

            for (int threadIndex = 0; threadIndex < numberOfThreads; threadIndex++)
            {
                threads.Add(new Thread(new ThreadStart(() =>
                {
                    ThreadTest(threadIndex);
                })));
            }
            threads.ForEach(n => n.Start());
            threads.ForEach(n => n.Join());
            Assert.IsFalse(reportLimitReached);
        }

        /// <summary>
        /// Test a initialization and submission sequence for backtrace client w/ threading w/o rate limiting
        /// </summary>
        [TestCase(1, 2)]
        [TestCase(5, 2)]
        [TestCase(10, 2)]
        [TestCase(1, 5)]
        [TestCase(5, 10)]
        [TestCase(5, 20)]
        [Test(Author = "Arthur Tu and Konrad Dysput", Description = "Test a initialization and submission sequence for backtrace client w/ threading w/o rate limiting")]
        public void ThreadedWithRateLimiting(int numberOfThread, uint rateLimiting)
        {
            //set rate limiting
            reportLimitReached = false;
            _backtraceClient.ChangeRateLimiting(rateLimiting);


            //set expected number of drop and request
            int expectedNumberofRequest = 4 * numberOfThread;
            int expectedNumberOfDropRequest = expectedNumberofRequest - (int)rateLimiting;

            List<Thread> threads = new List<Thread>();
            int totalSend = 0;
            int totalDrop = 0;

            _backtraceClient.OnClientReportLimitReached += () =>
            {
                totalDrop++;
            };

            for (int threadIndex = 0; threadIndex < numberOfThread; threadIndex++)
            {
                threads.Add(new Thread(new ThreadStart(() =>
                {
                    ThreadTest(threadIndex, totalSend);
                })));
            }

            threads.ForEach(n => n.Start());
            threads.ForEach(n => n.Join());

            Assert.AreEqual(totalSend, expectedNumberofRequest);
            Assert.AreEqual(totalDrop, expectedNumberOfDropRequest);
        }
    }
}
