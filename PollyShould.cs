using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Polly;
using Polly.CircuitBreaker;
using Xunit;
using Xunit.Sdk;

namespace PollyPlayground
{
    public class PollyShould
    {
        private SampleClient sampleClient;

        public PollyShould()
        {
            sampleClient = Substitute.For<SampleClient>();
            sampleClient.ExecuteRequest().Returns(
                x => { throw new Exception("1st execution"); },
                x => { throw new NullReferenceException("2nd execution"); },
                x => "Yes?",
                x => "Yes my lord!",
                x => "Leave me alone!");
        }

        [Fact]
        public void retry_until_success()
        {
            var result = Policy.Handle<Exception>()
                                .RetryForever()
                                .Execute(sampleClient.ExecuteRequest);
            result.Should().Be("Yes?");
        }

        [Fact]
        public void throw_exception_after_one_retry()
        {
            Action execution = () => Policy.Handle<Exception>()
                                            .Retry(1)
                                            .Execute(sampleClient.ExecuteRequest);
        }

        [Fact]
        public void retry_until_desired_message()
        {
            var expectedMessage = "Leave me alone!";
            var policyResult = Policy.Handle<Exception>()
                                    .OrResult<string>(message => ShouldRetryExecution(message, expectedMessage))
                                    .RetryForever()
                                    .ExecuteAndCapture(sampleClient.ExecuteRequest);
            policyResult.Result.Should().Be(expectedMessage);
            policyResult.Outcome.Should().Be(OutcomeType.Successful);
        }

        [Fact]
        public void manage_policy_failures()
        {
            var expectedMessage = "Undefined message";
            var policyResult = Policy.Handle<Exception>()
                                    .OrResult<string>(message => ShouldRetryExecution(message, expectedMessage))
                                    .Retry(20)
                                    .ExecuteAndCapture(sampleClient.ExecuteRequest);
            policyResult.Outcome.Should().Be(OutcomeType.Failure);
        }

        [Fact]
        public void wait_two_seconds_before_retry()
        {
            var result = Policy.Handle<Exception>()
                                .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(2))
                                .Execute(sampleClient.ExecuteRequest);
            result.Should().Be("Yes?");
        }

        [Fact]
        public void increase_await_time_before_every_retry()
        {
            var result = Policy.Handle<Exception>()
                                .WaitAndRetry(3, retryAttempt => TimeSpan.FromSeconds(2*retryAttempt))
                                .Execute(sampleClient.ExecuteRequest);
            result.Should().Be("Yes?");
        }

        [Fact]
        public void provide_circuit_breaker()
        {
            var breaker = Policy
                .Handle<Exception>()
                .CircuitBreaker(2, TimeSpan.FromHours(1));
            var result = breaker.ExecuteAndCapture(sampleClient.ExecuteRequest);
            result.FinalException.Should().BeOfType<Exception>();
            breaker.CircuitState.Should().Be(CircuitState.Closed);
            result = breaker.ExecuteAndCapture(sampleClient.ExecuteRequest);
            result.FinalException.Should().BeOfType<NullReferenceException>();
            breaker.CircuitState.Should().Be(CircuitState.Open);
            result = breaker.ExecuteAndCapture(sampleClient.ExecuteRequest);
            result.FinalException.Should().BeOfType<BrokenCircuitException>();
            breaker.Reset();
            breaker.CircuitState.Should().Be(CircuitState.Closed);
            result = breaker.ExecuteAndCapture(sampleClient.ExecuteRequest);
            result.Result.Should().Be("Yes?");
        }

        private bool ShouldRetryExecution(string message, string expectedMessage)
        {
            return message != expectedMessage;
        }
    }

    public interface SampleClient
    {
        string ExecuteRequest();
    }
}
