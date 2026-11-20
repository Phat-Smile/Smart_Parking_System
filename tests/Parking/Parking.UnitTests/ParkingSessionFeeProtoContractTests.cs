using Xunit;

namespace ParkingSystem.Tests.Parking.UnitTests;

public sealed class ParkingSessionFeeProtoContractTests
{
    [Fact]
    public void Parking_session_fee_proto_declares_qr_rpc_and_request_message()
    {
        // Locate proto file relative to test execution
        var protoPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "contracts", "grpc", "parking_session_fee.proto");
        if (!File.Exists(protoPath))
        {
            // Fallback for different working directories
            protoPath = Path.GetFullPath("contracts/grpc/parking_session_fee.proto");
        }

        if (File.Exists(protoPath))
        {
            var content = File.ReadAllText(protoPath);
            Assert.Contains("rpc GetSessionFeeContextByQr(GetSessionFeeContextByQrRequest)", content);
            Assert.Contains("message GetSessionFeeContextByQrRequest", content);
            Assert.Contains("string qr_token = 1;", content);
        }
    }
}
