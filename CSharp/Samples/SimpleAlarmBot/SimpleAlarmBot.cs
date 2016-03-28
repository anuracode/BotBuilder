﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;

using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Models;
using Newtonsoft.Json.Linq;

namespace Microsoft.Bot.Sample.SimpleAlarmBot
{
    [LuisModel("https://api.projectoxford.ai/luis/v1/application?id=c413b2ef-382c-45bd-8ff0-f76d60e2a821&subscription-key=fe054e042fd14754a83f0a205f6552a5&q=")]
    [Serializable]
    public class SimpleAlarmBot : LuisDialog, ISerializable
    {
        private readonly List<Alarm> alarms = new List<Alarm>();

        public const string DefaultAlarmWhat = "default";

        public bool TryFindAlarm(LuisResult result, out Alarm alarm)
        {
            alarm = null;

            string what;

            EntityRecommendation title;
            if (result.TryFindEntity(Entity_Alarm_Title, out title))
            {
                what = title.Entity;
            }
            else
            {
                what = DefaultAlarmWhat;
            }

            alarm = this.alarms.FirstOrDefault(a => a.What == what);
            return alarm != null;
        }

        private const string Entity_Alarm_Title = "builtin.alarm.title";
        private const string Entity_Alarm_Start_Time = "builtin.alarm.start_time";
        private const string Entity_Alarm_Start_Date = "builtin.alarm.start_date";

        [LuisIntent("")]
        public async Task None(IDialogContext context, LuisResult result)
        {
            string message = $"Sorry I did not understand: " + string.Join(", ", result.Intents.Select(i => i.Intent));
            await context.PostAsync(message);
            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.delete_alarm")]
        public async Task DeleteAlarm(IDialogContext context, LuisResult result)
        {
            Alarm alarm;
            if (TryFindAlarm(result, out alarm))
            {
                this.alarms.Remove(alarm);
                await context.PostAsync($"alarm {alarm} deleted");
            }
            else
            {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.find_alarm")]
        public async Task FindAlarm(IDialogContext context, LuisResult result)
        {
            Alarm alarm;
            if (TryFindAlarm(result, out alarm))
            {
                await context.PostAsync($"found alarm {alarm}");
            }
            else
            {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.set_alarm")]
        public async Task SetAlarm(IDialogContext context, LuisResult result)
        {
            EntityRecommendation title;
            if (! result.TryFindEntity(Entity_Alarm_Title, out title))
            {
                title = new EntityRecommendation(Entity_Alarm_Title) { Entity = DefaultAlarmWhat };
            }

            EntityRecommendation date;
            if (! result.TryFindEntity(Entity_Alarm_Start_Date, out date))
            {
                date = new EntityRecommendation() { Entity = string.Empty };
            }

            EntityRecommendation time;
            if (!result.TryFindEntity(Entity_Alarm_Start_Time, out time))
            {
                time = new EntityRecommendation() { Entity = string.Empty };
            }

            var parser = new Chronic.Parser();
            var span = parser.Parse(date.Entity + " " + time.Entity);

            if (span != null)
            {
                var when = span.Start ?? span.End;
                var alarm = new Alarm() { What = title.Entity, When = when.Value };
                this.alarms.Add(alarm);

                string reply = $"alarm {alarm} created";
                await context.PostAsync(reply);
            }
            else
            {
                await context.PostAsync("could not find time for alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.snooze")]
        public async Task AlarmSnooze(IDialogContext context, LuisResult result)
        {
            Alarm alarm;
            if (TryFindAlarm(result, out alarm))
            {
                alarm.When = alarm.When.Add(TimeSpan.FromMinutes(7));
                await context.PostAsync($"alarm {alarm} snoozed!");
            }
            else
            {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.time_remaining")]
        public async Task TimeRemaining(IDialogContext context, LuisResult result)
        {
            Alarm alarm;
            if (TryFindAlarm(result, out alarm))
            {
                var now = DateTime.UtcNow;
                if (alarm.When > now)
                {
                    var remaining = alarm.When.Subtract(DateTime.UtcNow);
                    await context.PostAsync($"There is {remaining} remaining for alarm {alarm}.");
                }
                else
                {
                    await context.PostAsync($"The alarm {alarm} expired already.");
                }
            }
            else
            {
                await context.PostAsync("did not find alarm");
            }

            context.Wait(MessageReceived);
        }

        private Alarm turnOff;

        [LuisIntent("builtin.intent.alarm.turn_off_alarm")]
        public async Task TurnOffAlarm(IDialogContext context, LuisResult result)
        {
            if (TryFindAlarm(result, out this.turnOff))
            {
                Prompts.Confirm(context, AfterConfirming_TurnOffAlarm, "Are you sure?");
            }
            else
            {
                await context.PostAsync("did not find alarm");
            }
        }

        public async Task AfterConfirming_TurnOffAlarm(IDialogContext context, IAwaitable<bool> confirmation)
        {
            if (await confirmation)
            {
                this.alarms.Remove(this.turnOff);
                await context.PostAsync($"Ok, alarm {this.turnOff} disabled.");
            }
            else
            {
                await context.PostAsync("Ok! We haven't modified your alarms!");
            }

            context.Wait(MessageReceived);
        }

        [LuisIntent("builtin.intent.alarm.alarm_other")]
        public async Task AlarmOther(IDialogContext context, LuisResult result)
        {
            await context.PostAsync("what ?");
            context.Wait(MessageReceived);
        }

        public SimpleAlarmBot()
        {
        }

        protected SimpleAlarmBot(SerializationInfo info, StreamingContext context)
        {
            var json = info.GetValue<string>(nameof(this.alarms));
            this.alarms = JArray.Parse(json).ToObject<List<Alarm>>();
            this.turnOff = info.GetValue<Alarm>(nameof(this.turnOff));
        }

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(this.alarms), JArray.FromObject(this.alarms).ToString());
            info.AddValue(nameof(this.turnOff), turnOff);
        }

        [Serializable]
        public sealed class Alarm : IEquatable<Alarm>
        {
            public DateTime When { get; set; }
            public string What { get; set; }

            public override string ToString()
            {
                return $"[{this.What} at {this.When}]";
            }

            public bool Equals(Alarm other)
            {
                return other != null
                    && this.When == other.When
                    && this.What == other.What;
            }

            public override bool Equals(object other)
            {
                return Equals(other as Alarm);
            }

            public override int GetHashCode()
            {
                return this.What.GetHashCode();
            }
        }
    }
}