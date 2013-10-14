// $Id: ChannelModeTypes.cs 373 2008-04-14 15:23:42Z tomoyo $
using System;

namespace Misuzilla.Net.Irc
{
	public enum ChannelModeTypes
	{
		ChannelCreator    = 'O',
		Operator          = 'o',
		Voice             = 'v',
		Anonymous         = 'a',
		InviteOnly        = 'i',
		Moderate          = 'm',
		NoMessagesOutside = 'n',
		Quiet             = 'q',
		Private           = 'p',
		Secret            = 's',
		Reop              = 'r',
		TopicByOperators  = 't',
		ChannelKey        = 'k',
		UserLimit         = 'l',
		ExceptionBan      = 'e',
		Ban               = 'b',
		Invitation        = 'I'
	}
}