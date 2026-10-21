using Core.Input;
public delegate void InputDataSender(ref PlayerInput input,float time);


public delegate void RefAction<T>(ref T val) where T:struct;
public delegate void RefAction<T1,T2>(ref T1 val1, ref T2 val2) where T1:struct where T2:struct;