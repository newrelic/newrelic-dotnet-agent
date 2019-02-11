#pragma once
#include <functional>

namespace NewRelic { namespace Profiler
{
	struct OnDestruction
	{
		std::function<void()> _onDestroyed;

		OnDestruction(std::function<void()> onDestroyed)
			: _onDestroyed(onDestroyed)
		{ }

		~OnDestruction()
		{
			_onDestroyed();
		}
	};
}}
