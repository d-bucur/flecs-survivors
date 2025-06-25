using System;
using System.Collections.Generic;
using Flecs.NET.Core;

interface IPropertyTween {
	void Tick(float delta, Entity ent);
	void Cleanup(Entity ent);
}

delegate P ComponentGetter<C, P>(ref C component);
delegate void ComponentSetter<C, P>(ref C component, P property);
delegate void EndCallback<C>(ref C comp);

record struct PropertyTween<C, P>(
	ComponentSetter<C, P> Setter,
	Func<float, P> F,
	ComponentGetter<C, P>? Getter = null,
	EndCallback<C>? OnEndCb = null
) : IPropertyTween {

	public void Tick(float delta, Entity ent) {
		var val = F(delta);
		ref var comp = ref ent.GetMut<C>();
		Setter(ref comp, val);
	}

	public void Cleanup(Entity ent) {
		OnEndCb?.Invoke(ref ent.GetMut<C>());
	}
}

record struct Tween(Entity target, float MaxTime = -1) {
	private float CurrentTime;
	private List<IPropertyTween> PropertyTweens = new();

	public void Tick(float delta) {
		CurrentTime += delta;
		foreach (var t in PropertyTweens) {
			t.Tick(CurrentTime, target);
		}
	}

	public bool HasFinished() {
		return MaxTime > 0 && CurrentTime > MaxTime;
	}

	public void Cleanup() {
		foreach (var p in PropertyTweens) {
			p.Cleanup(target);
		}
	}

	public void RegisterEcs() {
		target.CsWorld().Entity().Set(this);
	}

	public Tween With<C, P>(ComponentSetter<C, P> Setter, Func<float, P> F, ComponentGetter<C, P>? Getter = null, EndCallback<C>? OnEnd = null) {
		PropertyTweens.Add(new PropertyTween<C, P>(
			Setter,
			F,
			Getter,
			OnEnd
		));
		return this;
	}
}