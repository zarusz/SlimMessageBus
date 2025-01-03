namespace SlimMessageBus.Host.CircuitBreaker.HealthCheck.Test;

public static class HealthCheckBackgroundServiceTests
{
    public class AreEqualTests
    {
        [Fact]
        public void AreEqual_ShouldReturnTrue_WhenBothDictionariesAreEmpty()
        {
            // Arrange
            var dict1 = new Dictionary<string, int>();
            var dict2 = new Dictionary<string, int>();

            // Act
            var result = HealthCheckBackgroundService.AreEqual(dict1, dict2);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void AreEqual_ShouldReturnFalse_WhenDictionariesHaveDifferentCounts()
        {
            // Arrange
            var dict1 = new Dictionary<string, int> { { "key1", 1 } };
            var dict2 = new Dictionary<string, int>();

            // Act
            var result = HealthCheckBackgroundService.AreEqual(dict1, dict2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void AreEqual_ShouldReturnFalse_WhenDictionariesHaveDifferentKeys()
        {
            // Arrange
            var dict1 = new Dictionary<string, int> { { "key1", 1 } };
            var dict2 = new Dictionary<string, int> { { "key2", 1 } };

            // Act
            var result = HealthCheckBackgroundService.AreEqual(dict1, dict2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void AreEqual_ShouldReturnFalse_WhenDictionariesHaveDifferentValues()
        {
            // Arrange
            var dict1 = new Dictionary<string, int> { { "key1", 1 } };
            var dict2 = new Dictionary<string, int> { { "key1", 2 } };

            // Act
            var result = HealthCheckBackgroundService.AreEqual(dict1, dict2);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void AreEqual_ShouldReturnTrue_WhenDictionariesHaveSameKeysAndValues()
        {
            // Arrange
            var dict1 = new Dictionary<string, int> { { "key1", 1 } };
            var dict2 = new Dictionary<string, int> { { "key1", 1 } };

            // Act
            var result = HealthCheckBackgroundService.AreEqual(dict1, dict2);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void AreEqual_ShouldReturnTrue_WhenDictionariesAreComplexButEqual()
        {
            // Arrange
            var dict1 = new Dictionary<string, int>
            {
                { "key1", 1 },
                { "key2", 2 },
                { "key3", 3 }
            };
            var dict2 = new Dictionary<string, int>
            {
                { "key1", 1 },
                { "key2", 2 },
                { "key3", 3 }
            };

            // Act
            var result = HealthCheckBackgroundService.AreEqual(dict1, dict2);

            // Assert
            result.Should().BeTrue();
        }
    }

    public class PublishAsyncTests
    {
        [Fact]
        public async Task PublishAsync_ShouldUpdateEntries_WhenServiceIsActive()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, tags: ["tag1"]) },
                { "check2", new HealthReportEntry(HealthStatus.Degraded, "Degraded", TimeSpan.Zero, null, null, tags: ["tag2"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            // Act
            await target.PublishAsync(report, CancellationToken.None);

            // Assert
            var expectedTagStatus = new Dictionary<string, HealthStatus>
        {
            { "tag1", HealthStatus.Healthy },
            { "tag2", HealthStatus.Degraded }
        };

            target.TagStatus.Should().BeEquivalentTo(expectedTagStatus);
        }

        [Fact]
        public async Task PublishAsync_ShouldSetTagStatusToUnhealthy_WhenConflictingHealthStatusOccurs()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["sharedTag"]) },
                { "check2", new HealthReportEntry(HealthStatus.Unhealthy, "Unhealthy", TimeSpan.Zero, null, null, ["sharedTag"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            // Act
            await target.PublishAsync(report, CancellationToken.None);

            // Assert
            var tagStatus = target.TagStatus;
            tagStatus.Should().ContainKey("sharedTag").WhoseValue.Should().Be(HealthStatus.Unhealthy);
        }

        [Fact]
        public async Task PublishAsync_ShouldSetTagStatusToUnhealthy_WhenOneIsDegradedAndOtherIsUnHealthy()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Degraded, "Degraded", TimeSpan.Zero, null, null, ["sharedTag"]) },
                { "check2", new HealthReportEntry(HealthStatus.Unhealthy, "UnHealthy", TimeSpan.Zero, null, null, ["sharedTag"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            // Act
            await target.PublishAsync(report, CancellationToken.None);

            // Assert
            var tagStatus = target.TagStatus;
            tagStatus.Should().ContainKey("sharedTag").WhoseValue.Should().Be(HealthStatus.Unhealthy);
        }

        [Fact]
        public async Task PublishAsync_ShouldSetTagStatusToDegraded_WhenOneIsDegradedAndOtherIsHealthy_AndNoUnhealthyExists()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Degraded, "Degraded", TimeSpan.Zero, null, null, ["sharedTag"]) },
                { "check2", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["sharedTag"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            // Act
            await target.PublishAsync(report, CancellationToken.None);

            // Assert
            var tagStatus = target.TagStatus;
            tagStatus.Should().ContainKey("sharedTag").WhoseValue.Should().Be(HealthStatus.Degraded);
        }

        [Fact]
        public async Task PublishAsync_ShouldNotChangeTagStatus_WhenConflictingHealthStatusIsSame()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["sharedTag"]) },
                { "check2", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["sharedTag"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            // Act
            await target.PublishAsync(report, CancellationToken.None);

            // Assert
            var tagStatus = target.TagStatus;
            tagStatus.Should().ContainKey("sharedTag").WhoseValue.Should().Be(HealthStatus.Healthy);
        }

        [Fact]
        public async Task PublishAsync_ShouldNotCallDelegates_IfTagStatusHasNotChanged()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var delegateCalled = false;
            await target.Subscribe(
                _ =>
                {
                    delegateCalled = true;
                    return Task.CompletedTask;
                });

            var initialHealthReport = new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["tag1"]) }
                },
                TimeSpan.Zero);

            await target.PublishAsync(initialHealthReport, CancellationToken.None);
            delegateCalled = false;

            var unchangedHealthReport = new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["tag1"]) },
                { "check2", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["tag1"]) }
                },
                TimeSpan.Zero);

            // Act
            await target.PublishAsync(unchangedHealthReport, CancellationToken.None);

            // Assert
            delegateCalled.Should().BeFalse();
        }
    }

    public class SubscribeTests
    {
        [Fact]
        public async Task Subscribe_ShouldInvokeDelegateImmediatelyWithCurrentStatus()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            await target.StartAsync(CancellationToken.None);

            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["tag1"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);
            await target.PublishAsync(report, CancellationToken.None);

            IReadOnlyDictionary<string, HealthStatus>? capturedStatus = null;
            Task OnChange(IReadOnlyDictionary<string, HealthStatus> status)
            {
                capturedStatus = status;
                return Task.CompletedTask;
            }

            // Act
            await target.Subscribe(OnChange);

            // Assert
            capturedStatus.Should().NotBeNull();
            capturedStatus.Should().ContainKey("tag1").WhoseValue.Should().Be(HealthStatus.Healthy);
        }
    }

    public class UnsubscribeTests
    {
        [Fact]
        public async Task Unsubscribe_ShouldRemoveDelegate()
        {
            // Arrange
            using var target = new HealthCheckBackgroundService();
            var entries = new Dictionary<string, HealthReportEntry>
            {
                { "check1", new HealthReportEntry(HealthStatus.Healthy, "Healthy", TimeSpan.Zero, null, null, ["tag1"]) }
            };
            var report = new HealthReport(entries, TimeSpan.Zero);

            IReadOnlyDictionary<string, HealthStatus>? capturedStatus = null;
            Task OnChange(IReadOnlyDictionary<string, HealthStatus> status)
            {
                capturedStatus = status;
                return Task.CompletedTask;
            }

            // Act
            await target.Subscribe(OnChange);
            capturedStatus = null;

            target.Unsubscribe(OnChange);
            await target.PublishAsync(report, CancellationToken.None);

            // Assert
            capturedStatus.Should().BeNull();
        }
    }
}
