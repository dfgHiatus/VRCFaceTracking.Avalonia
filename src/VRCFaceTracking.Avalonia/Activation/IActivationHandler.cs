﻿using System.Threading.Tasks;

namespace VRCFaceTracking.Activation;

public interface IActivationHandler
{
    bool CanHandle(object args);

    Task HandleAsync(object args);
}
