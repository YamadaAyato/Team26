public interface ICarryState
{
    void OnInteract(CorpseCarrier carrier);
    void OnEnter(CorpseCarrier carrier) { }
    void OnExit(CorpseCarrier carrier) { }
}
