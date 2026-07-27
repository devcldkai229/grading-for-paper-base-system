using System.Threading.Tasks;
using NotificationService.Infrastructure.Consumers;
using Xunit;

namespace NotificationService.UnitTests
{
    public class NotificationIdempotencyGuardTests
    {
        [Fact]
        public async Task TryClaimAsync_WithoutRedisConfigured_AlwaysReturnsTrue()
        {
            var guard = new NotificationIdempotencyGuard(redis: null);

            var first = await guard.TryClaimAsync("some-key");
            var second = await guard.TryClaimAsync("some-key");

            Assert.True(first);
            Assert.True(second);
        }

        [Fact]
        public async Task ReleaseAsync_WithoutRedisConfigured_DoesNotThrow()
        {
            var guard = new NotificationIdempotencyGuard(redis: null);

            var exception = await Record.ExceptionAsync(() => guard.ReleaseAsync("some-key"));

            Assert.Null(exception);
        }
    }
}
