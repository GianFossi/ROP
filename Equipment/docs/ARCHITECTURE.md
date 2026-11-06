# Equipment — architettura e roadmap

Modello F# di un **apparecchio a pressione** come grafo di **componenti** (`Component`) collegati da **porte** (`Port`).
Scopo finale: esplorare l'apparecchio per verifiche dimensionali, calcolare pesi/volumi/ingombri, generare il 3D
(OpenCascade), disegnare le tavole 2D, e dare una UI WPF/MVVM con persistenza.

Convenzioni di unità (mai miste): lunghezze **mm**, volumi **mm³**, angoli **rad**, densità **kg/m³**, masse **kg**.

## Perché un grafo di porte (e non un albero puro)

Un albero non può rappresentare un fascio tubiero: i tubi partono da una piastra e **si riconnettono** all'altra
(tubi diritti) o **alla stessa piastra** (tubi a U). Il modello è quindi un grafo non orientato:

| Elemento | Significato |
|---|---|
| `Component` | parte parametrica (`Shape`) + materiale + `Supplied` + attributi. Non memorizza la posizione. |
| `Port` | punto di connessione sul componente: frame locale con **Z = normale uscente**, `Opens` = cavità interna con cui comunica, `MaxConnections` (None = illimitato, es. campo tubi). |
| `Connection` | accoppiamento porta A ↔ porta B: normali opposte, frame coincidenti, poi `Offset` (gioco assiale, rotazione/“clocking”). |
| `Equipment` | componenti + connessioni + `Anchors` (componenti posizionati esplicitamente, di norma uno). |

**Posizionamento parametrico.** La posizione di ogni componente *deriva* dalle connessioni: visita in ampiezza dagli
anchor; ogni arco usato per posizionare è un *tree edge*, ogni arco che porta a un nodo già posizionato è un
*closing edge* e viene **verificato geometricamente** (tolleranza 0.01 mm / 0.01°). Esempio: la lunghezza dei tubi deve
essere uguale a `sp. TS1 + L mantello + sp. TS2`, altrimenti `ClosingMismatch`. Modificare una quota sposta
automaticamente tutto il ramo a valle.

- Fascio diritto: `TB.End1 ↔ TS1.TubeField1`, `TB.End2 ↔ TS2.TubeField2` (ciclo: TS1–mantello–TS2–fascio).
- Fascio a U: `UB.EndA ↔ TS.TubeField1` e `UB.EndB ↔ TS.TubeField1` (due archi sulla **stessa** porta → ciclo di lunghezza 2).
- Ugelli: `CylindricalShell.Openings` generano porte radiali sulla superficie esterna.

## Moduli (`src/Equipment.Core`)

| File | Contenuto |
|---|---|
| `Geometry.fs` | `Vec3`, `Frame` (trasformazione rigida), `Aabb`, `Hull` (cerchi/punti estremi → bounding box esatto anche ruotato) |
| `Profile.fs` | profili (R,Z) con segmenti retti ed ellittici/circolari; volume e baricentro di rivoluzione (Pappo-Guldino con Green + Gauss-Legendre, esatto) |
| `Part.fs` | `Part` = volume + baricentro + hull; rivoluzione, box, curva a U (formula esatta) |
| `Issues.fs` | `ComponentId`, `ConnectionId`, `Issue` (messaggi tipati per `Returns<_, Issue>`) |
| `TubeLayout.fs` | generatori passo triangolare/quadro, corsia, accoppiamento tubi a U |
| `Shapes.fs` | `Shape` (mantello, tubo, cono, fondi ellittico/emisferico/torosferico, piatto, piastra tubiera, flangia, blocco, fasci) → `Body` (solidi, vuoti, cavità, porte) |
| `Component.fs` | `Component`, `MaterialRef` |
| `Equipment.fs` | grafo, `resolve`, operazioni: add / connect / disconnect / remove / update / offset / anchor |
| `Assembly.fs` | validazione, albero di esplorazione con archi di chiusura, rango dei cicli, cammini |
| `Properties.fs` | peso (solo fornitura), baricentro, ingombro e volume del parallelepipedo (in un frame di trasporto), **camere** |

Le operazioni di modifica restituiscono `Returns<Equipment, Issue>` (libreria ROP): errori in `Failure`, osservazioni
(componente non connesso, ramo ri-ancorato, anello che non chiude) come *warning*.

**Camere di fluido.** Ogni cavità appartiene a una camera; due cavità si fondono se unite da una connessione le cui
porte hanno entrambe `Opens`. Le piastre tubiere hanno cavità “interfaccia” di volume nullo (`Face1Side`/`Face2Side`):
così lato tubi (mantelli di testa + interno tubi) e lato mantello risultano camere distinte automaticamente.

## Approssimazioni note (da raffinare)

- Il volume delle camere è **lordo**: non detrae ancora i tubi che occupano il lato mantello (step 3).
- Fondo ellittico: superficie esterna = ellisse di semiassi (a+t, b+t) (non l'offset esatto).
- Aperture sul mantello: foro cilindrico piano (π/4·d²·t), nessun rinforzo né volume di cavità aggiunto.
- Piastra tubiera: fori a baricentro centrato; fascio diritto con baricentro dalla media delle posizioni.
- Il materiale è `MaterialRef { Code; Density }`: il collegamento al database Materials è previsto, non ancora fatto.

## Roadmap (step by step)

1. ✅ **Modello F# + grafo + proprietà** (questo step): componenti, connessioni, cicli, peso, baricentro, camere, ingombri.
2. **Motore 3D OpenCascade (F#)**: `Equipment.Engine3D`; binding minimale delle sole DLL OCCT necessarie
   (TKernel, TKMath, TKG2d/3d, TKGeomBase, TKBRep, TKGeomAlgo, TKTopAlgo, TKPrim, TKBO, TKShHealing, TKMesh, TKService/TKV3d solo se serve la vista);
   `Shape → TopoDS_Shape`, assemblaggio con i `Frame` risolti, tessellazione → mesh per la UI; **verifica incrociata**
   volume/baricentro OCC (`GProp`) vs. calcolo analitico di questo core.
3. **Volumi netti**: sottrazione dei tubi dalla camera lato mantello (finestra assiale), nomi/fluidi delle camere, passaggi.
4. **Persistenza**: DTO versionati `System.Text.Json`, migrazioni, salvataggio atomico; integrazione Materials/Databases/NumericInput.
5. **App WPF/MVVM (C#)**: viewport 3D con picking (click → componente), albero/proprietà, crea/modifica apparecchio, undo/redo.
6. **Disegno 2D**: tre viste (proiezioni dal modello 3D), quotatura automatica, cartiglio, export DXF/PDF.
