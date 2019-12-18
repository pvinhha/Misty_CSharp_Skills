using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MistyRobotics.Common;
using MistyRobotics.Common.Data;
using MistyRobotics.Common.Types;
using MistyRobotics.SDK;
using MistyRobotics.SDK.Messengers;
using MistyRobotics.SDK.Commands;
using MistyRobotics.SDK.Events;
using MistyRobotics.SDK.Responses;

namespace SkillLibrary
{
	/// <summary>
	/// Skill Template 
	/// You must implement the IMistySkill interface
	/// </summary>
	public class TellingJokeSkill : IMistySkill
	{
		/// <summary>
		/// Make a local variable to hold the misty robot interface, call it whatever you want 
		/// </summary>
		private IRobotMessenger _misty;
		
		/// <summary>
		/// Flag indicating class was disposed
		/// </summary>
		private bool _disposed = false;

		/// <summary>
		/// Random Generator for the move head, change led, and move arm values
		/// </summary>
		private Random _randomGenerator = new Random();

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to move the head
		/// </summary>
		private Timer _moveHeadTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to move the arms
		/// </summary>
		private Timer _moveArmsTimer;

		/// <summary>
		/// Timer object to perform callbacks at a regular interval to change the LED
		/// </summary>
		private Timer _ledTimer;

		/// <summary>
		/// Timer object to perform callbacks at a interval to process the data and do the next action
		/// </summary>
		private Timer _heartbeatTimer;

		/// <summary>
		/// Flag to indicate the proper time to talk
		/// </summary>
		private bool _timeToTellJokes = false;
		private bool _busyTalking = false;
		private string startedAudioName = "None";

		private string[] moodName = { "ok.wav" , "yeah.wav", "alright.wav" };
		private string[] jokeName = { "joke1.wav", "joke2.wav", "joke3.wav", "joke4.wav", "joke5.wav" };
		private string[] laughtName = { "laught1.wav", "laught2.wav", "laught3.wav" };
		private string[] eyeImages = { "e_DefaultContent.jpg" , "e_ContentLeft.jpg" , "e_ContentRight.jpg", "e_Joy.jpg",
										"e_Joy2.jpg", "e_Love.jpg"};
		private string[] commandKeyWords = { "hey", "joke", "more" };
		private int jokeIndex = 0;

		/// <summary>
		/// List of images on the robot
		/// </summary>
		private IList<ImageDetails> _imageList { get; set; } = new List<ImageDetails>();

		/// <summary>
		/// List of audio files on the robot
		/// </summary>
		private IList<AudioDetails> _audioList { get; set; } = new List<AudioDetails>();

		#region Required Skill Methods, Event Handlers & Accessors

		/// <summary>
		/// Skill details for the robot
		/// Currently you need a guid to distinguish your skill from others on the robot, get one at this link and paste it in
		/// https://www.guidgenerator.com/online-guid-generator.aspx
		/// 
		/// There are other parameters you can set if you want:
		///   Description - a description of your skill
		///   TimeoutInSeconds - timeout of skill in seconds
		///   StartupRules - a list of options to indicate if a skill should start immediately upon startup
		///   BroadcastMode - different modes can be set to share different levels of information from the robot using the 'SkillData' websocket
		///   AllowedCleanupTimeInMs - How long to wait after calling OnCancel before denying messages from the skill and performing final cleanup  
		/// </summary>
		public INativeRobotSkill Skill { get; private set; } = new NativeRobotSkill("TellingJokeSkill", "a365d72a-b9f1-4417-9315-ca0ce157df51")  // <<--- Changed this guid
		{
			TimeoutInSeconds = 60 * 5 //runs for 5 minutes or until the skill is cancelled
		};

		/// <summary>
		///	This method is called by the wrapper to set your robot interface
		///	You need to save this off in the local variable commented on above as you are going use it to call the robot
		/// </summary>
		/// <param name="robotInterface"></param>
		public void LoadRobotConnection(IRobotMessenger robotInterface)
		{
			_misty = robotInterface;
			_misty.SkillLogger.LogLevel = SkillLogLevel.Verbose;
		}

		/// <summary>
		/// This event handler is called when the robot/user sends a start message
		/// The parameters can be set in the Skill Runner (or as json) and used in the skill if desired
		/// </summary>
		/// <param name="parameters"></param>
		public async void OnStart(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
			int task_id = 0;
			_timeToTellJokes = true;
			try
			{
				//Get the audio and image lists for use in the skill
				_audioList = (await _misty.GetAudioListAsync())?.Data;
				_imageList = (await _misty.GetImageListAsync())?.Data;
				_misty.Wait(2000);

				task_id++;
				_misty.PlayAudio("Misty_Hi.wav", 80, null);
				_misty.Wait(2000);
				_misty.PlayAudio("Misty_I_am_Annie.wav", 80, null);
				_misty.Wait(4000);
				_misty.ChangeLED(255, 255, 255, null);
				_misty.DisplayImage("e_DefaultContent.jpg", 1, null);
				_misty.MoveHead(10, 0, 0, 60, AngularUnit.Degrees, null);

				task_id++;
				_misty.RegisterAudioPlayCompleteEvent(AudioPlayCallback, 0, true, null, null);
				// Temporarily disable this voice recognition of the keyphrase wake up words due to SDK software issue.
				// It will be used the the fix is done.
				// The skill will be initiated by the skill sequencer.
//				_misty.StartKeyPhraseRecognition(null);
//				_misty.RegisterKeyPhraseRecognizedEvent(10, false, "KeyPhrase", null);
//				_misty.KeyPhraseRecognizedEventReceived += ProcessKeyPhraseEvent;
				_misty.StartFaceRecognition(null);
				RegisterEvents();

				task_id++;
				_heartbeatTimer = new Timer(HeartbeatCallback, null, 5000, 3000);
				_moveHeadTimer = new Timer(MoveHeadCallback, null, 5000, 7000);
				_moveArmsTimer = new Timer(MoveArmCallback, null, 5000, 4000);
				_ledTimer = new Timer(ChangeLEDCallback, null, 1000, 1000);
			}
			catch (Exception ex)
			{
				if(task_id==0)
					_misty.SkillLogger.LogVerbose($"TellingJokeSkill : Failed to Load audio and image files");
				else if(task_id==1)
					_misty.SkillLogger.LogVerbose($"TellingJokeSkill : Failed to play audio and display image files");
				else if (task_id == 2)
					_misty.SkillLogger.LogVerbose($"TellingJokeSkill : Failed to register events");
				else
					_misty.SkillLogger.LogVerbose($"TellingJokeSkill : Failed to setup timers");
				_misty.SkillLogger.Log($"TellingJokeSkill : OnStart: => Exception", ex);
			}
		}

		/// <summary>
		/// This event handler is called when the cancel command is issued from the robot/user
		/// You currently have a few seconds to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		public void OnCancel(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
			_misty.SkillLogger.LogVerbose($"TellingJokeSkill : OnCancel called");
			_misty.ChangeLED(255, 0, 0, null);
			DoCleanup();
		}

		/// <summary>
		/// This event handler is called when the skill timeouts
		/// You currently have a few seconds to do cleanup and robot resets before the skill is shut down... 
		/// Events will be unregistered for you 
		/// </summary>
		public void OnTimeout(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
			_misty.SkillLogger.LogVerbose($"TellingJokeSkill : OnTimeout called");
			_misty.ChangeLED(0, 0, 255, null);
			DoCleanup();
		}

		/// <summary>
		/// This event handler is called when Pause is called on the skill
		/// User can save the skill status/data to be retrieved when Resume is called
		/// Infrastructure to help support this still under development, but users can implement this themselves as needed for now 
		/// </summary>
		/// <param name="parameters"></param>
		public void OnPause(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
			//In this template, Pause is not implemented by default and it simply calls OnCancel
			_misty.SkillLogger.LogVerbose($"TellingJokeSkill : OnPause called");
			OnCancel(sender, parameters);
		}

		/// <summary>
		/// This event handler is called when Resume is called on the skill
		/// User can restore any skill status/data and continue from Paused location
		/// Infrastructure to help support this still under development, but users can implement this themselves as needed for now 
		/// </summary>
		/// <param name="parameters"></param>
		public void OnResume(object sender, IDictionary<string, object> parameters)
		{
			//TODO Put your code here and update the summary above
			//In this template, Resume is not implemented by default and it simply calls OnStart
			_misty.SkillLogger.LogVerbose($"TellingJokeSkill : OnResume called");
			OnStart(sender, parameters);
		}
		
		/// <summary>
		/// Dispose method must be implemented by the class
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Protected Dispose implementation
		/// </summary>
		/// <param name="disposing"></param>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}
				
			if (disposing)
			{
				// Free any other managed objects here.
				_heartbeatTimer?.Dispose();
				_moveArmsTimer?.Dispose();
				_moveHeadTimer?.Dispose();
				_ledTimer?.Dispose();
			}

			// Free any unmanaged objects here.
			_disposed = true;
		}

		/// <summary>
		/// Skill Finalizer/Destructor
		/// </summary>
		~TellingJokeSkill()
		{
			Dispose(false);
		}
		#endregion

		#region User Created Callbacks

		/// <summary>
		/// Called on the timer tick to send a random move head command to the robot
		/// </summary>
		/// <param name="info"></param>
		private void MoveHeadCallback(object info)
		{
			_misty.MoveHead(_randomGenerator.Next(-20, 15), _randomGenerator.Next(-30, 30), _randomGenerator.Next(-60, 60), _randomGenerator.Next(10, 75), AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Called on the timer tick to send a random move arm command to the robot
		/// </summary>
		/// <param name="info"></param>
		private void MoveArmCallback(object info)
		{
			_misty.MoveArms(_randomGenerator.Next(-90, 90), _randomGenerator.Next(-90, 90), _randomGenerator.Next(10, 90), _randomGenerator.Next(10, 90), null, AngularUnit.Degrees, null);
		}

		/// <summary>
		/// Called on the timer tick to send a random change LED command to the robot
		/// </summary>
		/// <param name="info"></param>
		private void ChangeLEDCallback(object info)
		{
			_misty.ChangeLED((uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), (uint)_randomGenerator.Next(0, 256), null);
		}

		/// <summary>
		/// Called on the heartbeat timer tick to assess the current state of the audio commands and start telling jokes
		/// </summary>
		/// <param name="info"></param>
		public void HeartbeatCallback(object info)
		{
			string audioFile;
			if (!_misty.Wait(0)) { return; }
			if (_timeToTellJokes)
			{
				// Start telling a joke
				_timeToTellJokes = false;
				_busyTalking = true;
				audioFile = moodName[(uint)_randomGenerator.Next(0, 3)];
				if(AudioFileExist(audioFile))
					_misty.PlayAudio(audioFile, 60,null);
				else
					_misty.PlayAudio("s_Awe.wav", 60, null);

				_misty.Wait(3000);

				audioFile = jokeName[jokeIndex];
				if (AudioFileExist(audioFile))
					_misty.PlayAudio(audioFile, 60, null);
				else
					_misty.PlayAudio("s_Awe.wav", 60, null);

				WaitToCompleteAudioPlaying(audioFile);

				_misty.DisplayImage("e_EcstacyHilarious.jpg", 1, null);
				audioFile = laughtName[(uint)_randomGenerator.Next(0, 3)];
				if (AudioFileExist(audioFile))
					_misty.PlayAudio(audioFile, 60, null);
				else
					_misty.PlayAudio("s_Awe.wav", 60, null);

				_misty.Wait(4000);
				_misty.DisplayImage("e_DefaultContent.jpg", 1, null);

				jokeIndex++;
				if (jokeIndex > 4)
				{
					jokeIndex = 0;
					_timeToTellJokes = false;
					audioFile = "endjokes.wav";
					if (AudioFileExist(audioFile))
						_misty.PlayAudio(audioFile, 60, null);
					else
						_misty.PlayAudio("s_Awe.wav", 60, null);
					WaitToCompleteAudioPlaying(audioFile);
				}
				else
					_timeToTellJokes = true;
				_busyTalking = false;
			}
		}

		/// <summary>
		/// Callback called when the voice recognition "Hey Misty" event is triggered
		/// </summary>
		/// <param name="keyPhraseRecognizedEvent"></param>
		public void ProcessKeyPhraseEvent(object sender, IKeyPhraseRecognizedEvent keyPhraseRecognizedEvent)
		{
			_misty.DisplayImage("e_Love.jpg", 1, null);
			_misty.PlayAudio("Misty_Hi.wav", 60, null);

			if (!_misty.Wait(3000)) { return; }
			_misty.StartKeyPhraseRecognition(null);
			if (!_busyTalking)
				_timeToTellJokes = true;
			else
				_timeToTellJokes = false;
		}

		/// <summary>
		/// Callback called when a face is detected or recognized
		/// </summary>
		/// <param name="faceRecEvent"></param>
		private void FaceRecCallback(IFaceRecognitionEvent faceRecEvent)
		{
			string audioFile;
			if (_busyTalking)
				return;

			if (faceRecEvent.Label == "unknown person")
			{
				// Save randomness for future use
				// AudioDetails randomAudio = _audioList[_randomGenerator.Next(0, _audioList.Count - 1)];
				// ImageDetails randomImage = _imageList[_randomGenerator.Next(0, _imageList.Count - 1)];
				_misty.DisplayImage(eyeImages[_randomGenerator.Next(0, eyeImages.Length - 1)], 1, null);
				audioFile = "Misty_Hi.wav";
				if (AudioFileExist(audioFile))
					_misty.PlayAudio(audioFile, 80, null);
				else
					_misty.PlayAudio("s_Awe.wav", 80, null);
			}
			else
			{
				if (faceRecEvent.Label == "Daddy")
				{
					_misty.DisplayImage("e_EcstacyStarryEyed.jpg", 1, null);
					audioFile = "Misty_Hi_Daddy.wav";
				}
				else
				{
					_misty.DisplayImage("e_Joy.jpg", 1, null);
					audioFile = "Misty_Hi.wav";
				}

				if (AudioFileExist(audioFile))
					_misty.PlayAudio(audioFile, 80, null);
				else
					_misty.PlayAudio("s_Awe.wav", 80, null);
			}

			if (!_misty.Wait(5000)) { return; }
			_misty.DisplayImage("e_DefaultContent.jpg", 1, null);
			if (!_misty.Wait(5000)) { return; }
			_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);
		}

		/// <summary>
		/// Callback is called when an audio file complated playing
		/// </summary>
		/// <param name="robotEvent"></param>
		private void AudioPlayCallback(IAudioPlayCompleteEvent robotEvent)
		{
			_misty.SkillLogger.Log($"InteractiveMistySkill : AudioPlayCallback called for audio file {(robotEvent).Name}");
			startedAudioName = robotEvent.Name;
		}

		/// <summary>
		/// Wait for audio playing to complete
		/// </summary>
		/// <param name="waitOnAudioFile"></param>
		private void WaitToCompleteAudioPlaying(string waitOnAudioFile)
		{
			int timeOut = 3;
			while(timeOut > 0)
			{
				if (!startedAudioName.Equals(waitOnAudioFile))
				{
					_misty.Wait(1000);
					timeOut--;
				}
			}
			_misty.Wait(9000);
		}

		/// <summary>
		/// Audio file validation
		/// </summary>
		/// <param name="AudioFilename"></param>
		private bool AudioFileExist(string AudioFilename)
		{
			for(int i=0; i<_audioList.Count;i++)
			{
				AudioDetails inspectingAudio = _audioList[i];
				if (AudioFilename.Equals(inspectingAudio.Name))
					return true;
			}
			return false;
		}

		/// <summary>
		/// Image file validation
		/// </summary>
		/// <param name="ImageFilename"></param>
		private bool ImageFileExist(string ImageFilename)
		{
			for (int i = 0; i < _imageList.Count; i++)
			{
				ImageDetails inspectingImage = _imageList[i];
				if (ImageFilename.Equals(inspectingImage.Name))
					return true;
			}
			return false;
		}

		#endregion
		#region User Created Helper Methods

		/// <summary>
		/// Do timeout or cancel cleanup
		/// </summary>
		private void DoCleanup()
		{
			_misty.Stop(null);
			_misty.StopKeyPhraseRecognition(null);
			_misty.StopFaceRecognition(null);
			// Unregisters all events
			_misty.UnregisterAllEvents(UnregisterCallback);
			_misty.DisplayImage("e_DefaultContent.jpg", 1, null);
		}
		private void UnregisterCallback(IRobotCommandResponse response)
		{
			// Writes a message to the skill's log file.
			_misty.SkillLogger.Log($"InteractiveMistySkill : UnregisterCallback called");
		}

		#endregion
		#region User Created Helper Methods

		/// <summary>
		/// Register the desired startup events, more may be registered separately as needed
		/// </summary>
		private void RegisterEvents()
		{
			//Face rec
			_misty.RegisterFaceRecognitionEvent(FaceRecCallback, 0, false, null, null, null);
		}

		#endregion

	}
}