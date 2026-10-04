# RimJoust

Medieval tournaments for RimWorld 1.6: jousting, grand melee, archery, and the full three-event Grand Tournament. Very Game of Thrones.

[Steam Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=3743710674)

## What it does

- Jousting tournaments with single-elimination brackets, an opening ceremony, squires, a crowd, guest knights from other factions, betting, and a Queen of Love and Beauty.
- A grand melee: fence an arena, hand out blunted maces, last one standing wins.
- An archery contest: a shooting line, a straw butt, three volleys, loaner bows for everyone.
- The Grand Tournament chains all three events and crowns an overall champion on points.
- Everything runs on markers you place, so the arena layout is yours.

## Requirements

- RimWorld 1.6
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077)
- [Giddy-Up 2 (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3674332861)

## Building from source

Open `Source/RimJoust.csproj` and build in Release, or:

```
dotnet build Source/RimJoust.csproj -c Release
```

The assembly lands in `1.6/Assemblies/`. Copy the mod folder (everything except `Source/`) into your RimWorld `Mods` directory.

## Translations

The base language is English, with French shipped as an example. Translations are welcome as pull requests: add a `Languages/<YourLanguage>/` folder mirroring the structure of `Languages/French/`.

## Bug reports

Open an issue here or comment on the Workshop page. A log (with [HugsLib](https://steamcommunity.com/sharedfiles/filedetails/?id=818773962) or the vanilla dev console) helps a lot.

## Credits

Textures by Tomson Johnson. Code and everything else by BenFD.

## License

MIT. Do what you want with it, credit is appreciated.
