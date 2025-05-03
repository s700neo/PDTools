using System;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Sockets;
using System.Net;
using System.Numerics;
using PDTools.SimulatorInterface;
using PDTools.Crypto.SimulationInterface;

namespace PDTools.SimulatorInterfaceTestTool
{
	internal class Program
	{
		private static bool _showUnknown = false;
		private static UdpClient udpClient = new UdpClient();
		private static bool nocheckStatus = false;


		static async Task Main(string[] args)
		{
			Console.WriteLine("Simulator Interface GT7/GTSport/GT6");
			Console.WriteLine();

			if (args.Length == 0)
			{
				Console.WriteLine("Usage: SimulatorInterface.exe <IP address of PS3/PS4/PS5> ('--gtsport' for GT Sport support, '--gt6' for GT6 support)");
				return;
			}

			_showUnknown = args.Contains("--debug");
			bool gtsport = args.Contains("--gtsport");
			bool gt6 = args.Contains("--gt6");
			nocheckStatus = args.Contains("--nocheck");
			//nocheckStatus = args.Contains("--gtsport") || args.Contains("--gt6");

			if (gtsport && gt6)
			{
				Console.WriteLine("Error: Both GT6 and GT Sport arguments are present.");
				return;
			}

			SimulatorInterfaceGameType type = SimulatorInterfaceGameType.GT7;
			if (gtsport)
				type = SimulatorInterfaceGameType.GTSport;
			else if (gt6)
				type = SimulatorInterfaceGameType.GT6;

			Console.WriteLine("Starting interface..");

			SimulatorInterfaceClient simInterface = new SimulatorInterfaceClient(args[0], type);
			simInterface.OnReceive += SimInterface_OnReceive;

			var cts = new CancellationTokenSource();
			var task = simInterface.Start(cts.Token);

			try
			{
				await task;
			}
			catch (OperationCanceledException)
			{
				Console.WriteLine("Simulator Interface ending..");
			}
			catch (Exception e)
			{
				Console.WriteLine($"Errored during simulation: {e.Message}");
			}
			finally
			{
				simInterface.Dispose();
				udpClient.Dispose();
			}
		}

		private static void SimInterface_OnReceive(SimulatorPacket packet)
		{
			if (nocheckStatus)
			{
				SendTelemetry(packet);
			}
			if ((packet.Flags & SimulatorFlags.CarOnTrack) != 0 &&
			(packet.Flags & SimulatorFlags.Paused) == 0 &&
			(packet.Flags & SimulatorFlags.LoadingOrProcessing) == 0)
			{
				Console.SetCursorPosition(0, 0);
				packet.PrintPacket(_showUnknown);
				SendTelemetry(packet);
			}
		}

		private static void SendTelemetry(SimulatorPacket packet)
		{
			IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 20777);

			byte[] data = FormatTelemetryData(packet.Position, packet.Velocity, packet.Rotation);

			try
			{
				udpClient.Send(data, data.Length, remoteEndPoint);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Failed to send UDP packet: {ex.Message}");
			}
		}

		private static byte[] FormatTelemetryData(Vector3 position, Vector3 velocity, Quaternion rotation)
		{
			byte[] data = new byte[40];

			// Position
			Buffer.BlockCopy(BitConverter.GetBytes(position.X), 0, data, 0, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.Y), 0, data, 4, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(position.Z), 0, data, 8, 4);

			// Rotation (Quaternion)
			Buffer.BlockCopy(BitConverter.GetBytes(-rotation.X), 0, data, 12, 4); // NEGATE X
			Buffer.BlockCopy(BitConverter.GetBytes(-rotation.Y), 0, data, 16, 4); // NEGATE Y
			Buffer.BlockCopy(BitConverter.GetBytes(rotation.Z), 0, data, 20, 4);  // KEEP Z
			Buffer.BlockCopy(BitConverter.GetBytes(rotation.W), 0, data, 24, 4);  // KEEP W

			// Velocity in world space
			Buffer.BlockCopy(BitConverter.GetBytes(velocity.X), 0, data, 28, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(velocity.Y), 0, data, 32, 4);
			Buffer.BlockCopy(BitConverter.GetBytes(-velocity.Z), 0, data, 36, 4);	// NEGATE Z

			return data;
		}
	}
}
